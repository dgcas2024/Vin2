using System;
using System.Collections.Concurrent;
using System.Threading.Tasks;
using Vin2Api;
using Microsoft.Extensions.Hosting;
using System.Linq;
using Newtonsoft.Json;
using System.Collections.Generic;
using RestSharp;
using Zennolab.CapMonsterCloud;
using Zennolab.CapMonsterCloud.Requests;
using System.Security.Principal;
using Microsoft.Extensions.Configuration;

namespace Vin2
{
    public class Vin2BookingConfigs
    {
        public string TakeTokenTime { get; set; }
        public string ResolveCaptchaTime { get; set; }
        public string SubmitRequestTime { get; set; }
        public int? SubmitRequestTimespan { get; set; }
        public int? XRequest { get; set; }

        [JsonIgnore]
        public DateTime TakeTokenTimeCS => TimeSpan.TryParse(TakeTokenTime, out var rs) ? DateTime.Now.Date.Add(rs) : DateTime.Now.Date.AddDays(1).AddSeconds(-1);

        [JsonIgnore]
        public DateTime ResolveCaptchaTimeCS => TimeSpan.TryParse(ResolveCaptchaTime, out var rs) ? DateTime.Now.Date.Add(rs) : DateTime.Now.Date.AddDays(1).AddSeconds(-1);

        [JsonIgnore]
        public DateTime SubmitRequestTimeCS => TimeSpan.TryParse(SubmitRequestTime, out var rs) ? DateTime.Now.Date.Add(rs) : DateTime.Now.Date.AddDays(1).AddSeconds(-1);
    }

    public class Vin2Booking
    {
        private class SubmitBookingItem
        {
            public string Username { get; internal set; }
            public string ApartmentCode { get; internal set; }
            public Vin2BookingDataModel Vin2BookingDataModel { get; set; }

            public RestClient RestClient { get; internal set; }
            public Vin2Request.SubmitBookingSlot SubmitBookingSlot { get; set; }
            public DateTime ScheduleSubmitTime { get; set; }

            public DateTime? ActualSubmitTime { get; set; }
            public Task ResponseTask { get; set; }
        }

        private class CaptchaItem
        {
            public string Captcha { get; set; }
            public DateTime ExpiredTime { get; set; }
        }

        private readonly IHostEnvironment _hostEnvironment;
        private readonly Vin2Request _vin2Request;
        private readonly Vin2BookingData _vin2BookingData;
        private readonly Vin2Account _vin2Account;
        private readonly IVin2Message _vin2Message;
        private readonly Vin2Messenger _vin2Messenger;
        private readonly IConfiguration _configuration;

        private readonly ICapMonsterCloudClient _capMonsterCloudClient = null;
        public Vin2Booking(IVin2Message vin2Message, IHostEnvironment hostEnvironment, Vin2Request vin2Request, Vin2BookingData vin2BookingData, Vin2Account vin2Account, Vin2Messenger vin2Messenger, IConfiguration configuration)
        {
            _vin2Message = vin2Message;
            _hostEnvironment = hostEnvironment;
            _vin2Request = vin2Request;
            _vin2BookingData = vin2BookingData;
            _vin2Account = vin2Account;
            _vin2Messenger = vin2Messenger;
            _configuration = configuration;

            var clientOptions = new ClientOptions
            {
                ClientKey = _vin2Request.ReCaptcha_CapMonsterKey
            };
            _capMonsterCloudClient = CapMonsterCloudClientFactory.Create(clientOptions);

            _ = TakeBookingConfigsAsync();
        }

        public bool IsRunning { get; set; }
        public Vin2BookingConfigs Vin2BookingConfigs { get; set; }
        private ConcurrentQueue<CaptchaItem> Vin2ReCaptchaStorage { get; } = [];
        private ConcurrentDictionary<string, HashSet<long>> Vin2BookingWrongAccountWithUtility { get; } = [];
        private ConcurrentDictionary<string, SubmitBookingItem> Vin2BookingDone { get; } = [];
        private ConcurrentDictionary<string, SubmitBookingItem> Vin2BookingRemake { get; } = [];

        public async Task Save(string bookingConfigRaw)
        {
            await System.IO.File.WriteAllTextAsync($"{_hostEnvironment.EnvironmentName}--booking-configs.txt", bookingConfigRaw);
            await TakeBookingConfigsAsync();
            _vin2Messenger.NotifyUpdateBookingInfo();
        }

        public async Task TakeBookingConfigsAsync()
        {
            var bookingConfigRaw = await System.IO.File.ReadAllTextAsync($"{_hostEnvironment.EnvironmentName}--booking-configs.txt");
            Vin2BookingConfigs = bookingConfigRaw.DeserializeObject<Vin2BookingConfigs>() ?? new Vin2BookingConfigs();
        }

        public async Task ResolveReCaptchaAsync(int index, DateTime submitRequestTime)
        {
            _vin2Message.Info($"[{index}]. Taking recaptcha...");
            var recaptchaV2Request = new RecaptchaV2ProxylessRequest
            {
                WebsiteUrl = _vin2Request.Host,
                WebsiteKey = _vin2Request.ReCaptchaKey,
            };
            try
            {
                var captcha = await _capMonsterCloudClient.SolveAsync(recaptchaV2Request);
                if (captcha.Error != null)
                {
                    _vin2Message.Error($"[{index}]. Could not solve the recaptcha: {captcha.Error}");
                    return;
                }
                if ((submitRequestTime - DateTime.Now).TotalSeconds <= 115)
                {
                    _vin2Message.Success($"[{index}]. Taked recaptcha");
                    Vin2ReCaptchaStorage.Enqueue(new CaptchaItem
                    {
                        Captcha = captcha.Solution.Value,
                        ExpiredTime = DateTime.Now.AddSeconds(115)
                    });
                }
                else
                {
                    _vin2Message.Warn($"[{index}]. ReTaking recaptcha...");
                    await ResolveReCaptchaAsync(index, submitRequestTime);
                }
            }
            catch(Exception ex)
            {
                _vin2Message.Error($"[{index}]. Could not solve the recaptcha: {ex.Message}");
            }
        }

        public async void TakeReCaptchaAsync(DateTime submitRequestTime, int totalBooking)
        {
            _vin2Message.Info($"Begin take recaptcha...");
            var tasks = Enumerable.Repeat(0, totalBooking).Select(async (x, index) =>
            {
                await ResolveReCaptchaAsync(index, submitRequestTime);
            });
            await Task.WhenAll(tasks);
            _vin2Message.Success($"End take recaptcha.");
        }

        private SubmitBookingItem[] MakeSubmitBookingItems(DateTime bookingDate, Vin2AccountModel[] accountArray, Vin2BookingDataModel[] bookingArray,
            DateTime submitRequestTime, int submitRequestTimespan, int xRequest)
        {
            var totalBooking = Math.Min(bookingArray.Length, accountArray.Length) * xRequest;
            var delayTime = submitRequestTimespan / xRequest;

            var bookingArrayWithIndex = bookingArray.GroupBy(k => k.TimeAndPlaceText, g => g, (k, g) => new
            {
                TimeAndPlaceText = k,
                Delta = delayTime / g.Count(),
                Array = g.Select((gi, idx) => new
                {
                    Index = idx,
                    Data = gi
                })
            }).SelectMany(x => x.Array.Select(xi => new
            {
                xi.Index,
                x.Delta,
                xi.Data
            }));

            var submitBookingItems = bookingArrayWithIndex.Select((bookingDataItem, index) =>
            {
                List<SubmitBookingItem> submitBookingItemByAccount = [];
                if (accountArray.Length > index)
                {
                    var account = accountArray[index];
                    for (var xIndex = 0; xIndex < xRequest; xIndex++)
                    {
                        var submitBookingSlot = _vin2Request.MakeSubmitBookingSlot(account.Token, bookingDate.Date.CsLocalToJsUtc());
                        var restClient = submitBookingItemByAccount.FirstOrDefault(x => x.Username == account.Username)?.RestClient;
                        if (restClient == null)
                        {
                            var options = new RestClientOptions(_vin2Request.Host)
                            {
                                MaxTimeout = _vin2Request.RequestTimeout,
                                UserAgent = _vin2Request.UserAgent
                            };
                            restClient = new RestClient(options);
                            restClient.AddDefaultHeader("Connection", "keep-alive");
                        }
                        var submitBookingItem = new SubmitBookingItem
                        {
                            Username = account.Username,
                            ApartmentCode = account.ApartmentCode,
                            Vin2BookingDataModel = bookingDataItem.Data,

                            RestClient = restClient,
                            SubmitBookingSlot = submitBookingSlot,
                            ScheduleSubmitTime = submitRequestTime.AddMilliseconds(delayTime * xIndex + bookingDataItem.Index * bookingDataItem.Delta),

                            ActualSubmitTime = null,
                            ResponseTask = null
                        };
                        submitBookingItemByAccount.Add(submitBookingItem);
                    }
                    _vin2Message.Info($"{bookingDataItem.Data.TimeAndPlaceText} - Make booking item done for account: {account.Username}");
                }
                else
                {
                    _vin2Message.Warn($"{bookingDataItem.Data.TimeAndPlaceText} - TotalToken ({accountArray.Length}) < BookingArray ({bookingArray.Length})");
                }
                return submitBookingItemByAccount;
            }).ToArray();
            var flatData = submitBookingItems.SelectMany(x => x).OrderBy(x => x.ScheduleSubmitTime).ToArray();
            _vin2Message.Info($"--- Make booking item done: {flatData.Length}/{totalBooking} ---");
            flatData.OrderBy(x => x.Username).ThenBy(x => x.ScheduleSubmitTime).ToList().ForEach(x =>
            {
                _vin2Message.Info($"{x.Username} - {x.Vin2BookingDataModel.TimeAndPlaceText} - {x.ScheduleSubmitTime:HH:mm:ss.fff}");
            });
            _vin2Message.Info($"--- Make booking item done: {flatData.Length}/{totalBooking} ---");
            return flatData;
        }

        private async Task ExecuteSubmitBookingItemsAsync(SubmitBookingItem[] submitBookingItems)
        {
            var submitBookingItemByAccounts = submitBookingItems.GroupBy(k => k.Username, g => g, (k, g) => new
            {
                Username = k,
                SubmitBookingItems = g.OrderBy(x => x.ScheduleSubmitTime).ToArray()
            });
            var taskByAccounts = submitBookingItemByAccounts.Select(async x =>
            {
                var first = x.SubmitBookingItems[0];
                var left = first.ScheduleSubmitTime - DateTime.Now;
                if (left.Ticks > 0)
                {
                    await Task.Delay(left);
                }
                foreach (var submitBookingItem in x.SubmitBookingItems) 
                {
                    var _left = submitBookingItem.ScheduleSubmitTime - DateTime.Now;
                    if (_left.Ticks > 0)
                    {
                        await Task.Delay(_left);
                    }
                    var isDone = Vin2BookingDone.ContainsKey(submitBookingItem.Vin2BookingDataModel.TimeAndPlaceText);
                    var isWrongAccountWithUtility = Vin2BookingWrongAccountWithUtility.TryGetValue(submitBookingItem.Username, out HashSet<long> value) && value.Contains(submitBookingItem.Vin2BookingDataModel.UtilityId);
                    if (isDone || isWrongAccountWithUtility)
                    {
                        var processing = submitBookingItems
                            .Where(x => !Vin2BookingDone.ContainsKey(x.Vin2BookingDataModel.TimeAndPlaceText))
                            .Where(x => !Vin2BookingWrongAccountWithUtility.ContainsKey(submitBookingItem.Username) || !Vin2BookingWrongAccountWithUtility[submitBookingItem.Username].Contains(x.Vin2BookingDataModel.UtilityId))
                            .Where(x => !Vin2BookingRemake.ContainsKey(x.Vin2BookingDataModel.TimeAndPlaceText))
                            .OrderBy(x => Guid.NewGuid()).FirstOrDefault();
                        processing ??= submitBookingItems
                            .Where(x => !Vin2BookingDone.ContainsKey(x.Vin2BookingDataModel.TimeAndPlaceText))
                            .Where(x => !Vin2BookingWrongAccountWithUtility.ContainsKey(submitBookingItem.Username) || !Vin2BookingWrongAccountWithUtility[submitBookingItem.Username].Contains(x.Vin2BookingDataModel.UtilityId))
                            .OrderBy(x => Guid.NewGuid()).FirstOrDefault();
                        if (processing == null)
                        {
                            _vin2Message.Warn($"Ignore booking D/W: {isDone}/{isWrongAccountWithUtility} - {submitBookingItem.Username} - {submitBookingItem.Vin2BookingDataModel.TimeAndPlaceText}");
                            continue;
                        }
                        else
                        {
                            var oldTimeAndPlaceText = submitBookingItem.Vin2BookingDataModel.TimeAndPlaceText;
                            submitBookingItem.Vin2BookingDataModel = processing.Vin2BookingDataModel;
                            Vin2BookingRemake.TryAdd(submitBookingItem.Vin2BookingDataModel.TimeAndPlaceText, submitBookingItem);
                            _vin2Message.Warn($"Remake booking D/W: {isDone}/{isWrongAccountWithUtility} - {submitBookingItem.Username} - {oldTimeAndPlaceText} -> {processing.Vin2BookingDataModel.TimeAndPlaceText} ({processing.Username})");
                        }
                    }
                    CaptchaItem captchaItem = null;
                    while(true)
                    {
                        if (Vin2ReCaptchaStorage.TryDequeue(out captchaItem) && captchaItem.ExpiredTime > DateTime.Now)
                        {
                            makeRequestAndSubmit:
                            var actualSubmitTime = DateTime.Now;
                            submitBookingItem.ActualSubmitTime = actualSubmitTime;
                            var restRequest = submitBookingItem.SubmitBookingSlot.MakeSubmitBookingRequest(submitBookingItem.Vin2BookingDataModel.UtilityId, submitBookingItem.Vin2BookingDataModel.TimeConstraintId, submitBookingItem.Vin2BookingDataModel.PlaceId, captchaItem.Captcha, submitBookingItem.Vin2BookingDataModel.cs);
                            var responseTask = submitBookingItem.RestClient.ExecuteAsync(restRequest);
                            submitBookingItem.ResponseTask = responseTask;
                            var restResponse = await responseTask;
                            var responseTime = DateTime.Now - actualSubmitTime;
                            var content = restResponse.Content ?? "";
                            Vin2BookingRemake.TryRemove(submitBookingItem.Vin2BookingDataModel.TimeAndPlaceText, out _);
                            if (content.Contains("rejected", StringComparison.CurrentCultureIgnoreCase))
                            {
                                //if (DateTime.Now < captchaItem.ExpiredTime)
                                //{
                                //    _vin2Message.Warn($"SubmitBooking fail - {submitBookingItem.Username}: [{submitBookingItem.ScheduleSubmitTime:HH:mm:ss.fff}-{actualSubmitTime:HH:mm:ss.fff}--{(int)responseTime.TotalMilliseconds}] - {submitBookingItem.Vin2BookingDataModel.TimeAndPlaceText} - {restResponse.ResponseStatus} - {restResponse.ErrorMessage ?? ""} - TrySubmit0");
                                //    await Task.Delay(15000);
                                //    goto makeRequestAndSubmit;
                                //}
                                _vin2Message.Warn($"SubmitBooking fail - {submitBookingItem.Username}: [{submitBookingItem.ScheduleSubmitTime:HH:mm:ss.fff}-{actualSubmitTime:HH:mm:ss.fff}--{(int)responseTime.TotalMilliseconds}] - {submitBookingItem.Vin2BookingDataModel.TimeAndPlaceText} - {restResponse.ResponseStatus} - {restResponse.ErrorMessage ?? ""} - StopTrySubmit0");
                                break;
                            }
                            if (content.Contains("transactionId", StringComparison.CurrentCultureIgnoreCase) || content.Contains("Tiện ích đã hết chỗ", StringComparison.CurrentCultureIgnoreCase))
                            {
                                Vin2BookingDone.TryAdd(submitBookingItem.Vin2BookingDataModel.TimeAndPlaceText, submitBookingItem);
                            }
                            else if (content.Contains("căn hộ", StringComparison.CurrentCultureIgnoreCase))
                            {
                                Vin2BookingWrongAccountWithUtility.AddOrUpdate(submitBookingItem.Username, k => [submitBookingItem.Vin2BookingDataModel.UtilityId], (k, v) =>
                                {
                                    v.Add(submitBookingItem.Vin2BookingDataModel.UtilityId);
                                    return v;
                                });
                            }
                            if (restResponse.IsSuccessful)
                            {
                                _vin2Message.Info($"SubmitBooking done - {submitBookingItem.Username} - {submitBookingItem.ApartmentCode}: [{submitBookingItem.ScheduleSubmitTime:HH:mm:ss.fff}-{actualSubmitTime:HH:mm:ss.fff}--{(int)responseTime.TotalMilliseconds}] - {submitBookingItem.Vin2BookingDataModel.TimeAndPlaceText} - {content}");
                            }
                            else
                            {
                                _vin2Message.Error($"SubmitBooking fail - {submitBookingItem.Username}: [{submitBookingItem.ScheduleSubmitTime:HH:mm:ss.fff}-{actualSubmitTime:HH:mm:ss.fff}--{(int)responseTime.TotalMilliseconds}] - {submitBookingItem.Vin2BookingDataModel.TimeAndPlaceText} - {restResponse.ResponseStatus} - {restResponse.ErrorMessage ?? ""} - {content}");
                                if (restResponse.StatusCode == System.Net.HttpStatusCode.RequestTimeout || restResponse.StatusCode == System.Net.HttpStatusCode.GatewayTimeout ||
                                    content.Contains("cloudflare", StringComparison.CurrentCultureIgnoreCase) || content.Contains("Service Unavailable", StringComparison.CurrentCultureIgnoreCase))
                                {
                                    if (DateTime.Now < captchaItem.ExpiredTime)
                                    {
                                        _vin2Message.Warn($"SubmitBooking fail - {submitBookingItem.Username}: [{submitBookingItem.ScheduleSubmitTime:HH:mm:ss.fff}-{actualSubmitTime:HH:mm:ss.fff}--{(int)responseTime.TotalMilliseconds}] - {submitBookingItem.Vin2BookingDataModel.TimeAndPlaceText} - {restResponse.ResponseStatus} - {restResponse.ErrorMessage ?? ""} - TrySubmit");
                                        await Task.Delay(10);
                                        goto makeRequestAndSubmit;
                                    }
                                    _vin2Message.Warn($"SubmitBooking fail - {submitBookingItem.Username}: [{submitBookingItem.ScheduleSubmitTime:HH:mm:ss.fff}-{actualSubmitTime:HH:mm:ss.fff}--{(int)responseTime.TotalMilliseconds}] - {submitBookingItem.Vin2BookingDataModel.TimeAndPlaceText} - {restResponse.ResponseStatus} - {restResponse.ErrorMessage ?? ""} - StopTrySubmit");
                                }
                            }
                            break;
                        }
                        if (Vin2ReCaptchaStorage.IsEmpty)
                        {
                            _vin2Message.Error($"{submitBookingItem.Vin2BookingDataModel.TimeAndPlaceText} - Take captcha FAIL for account {submitBookingItem.Username}");
                            break;
                        }
                    }
                }
                _vin2Message.Info($"SubmitBooking done for ACCOUNT: {x.Username}");
            });
            await Task.WhenAll(taskByAccounts);
            _vin2Message.Info($"SubmitBooking ALL done");
        }

        private async Task XExecuteSubmitBookingItemsAsync(SubmitBookingItem[] submitBookingItems)
        {
            var executeSubmitBookingItems = submitBookingItems.OrderBy(x => x.ScheduleSubmitTime).Select(async submitBookingItem =>
            {
                CaptchaItem captchaItem = null;
                while (true)
                {
                    if (Vin2ReCaptchaStorage.TryDequeue(out captchaItem) && captchaItem.ExpiredTime > DateTime.Now)
                    {
                        var _left = submitBookingItem.ScheduleSubmitTime - DateTime.Now;
                        if (_left.Ticks > 0)
                        {
                            await Task.Delay(_left);
                        }
                        var actualSubmitTime = DateTime.Now;
                        submitBookingItem.ActualSubmitTime = actualSubmitTime;
                        var restRequest = submitBookingItem.SubmitBookingSlot.MakeSubmitBookingRequest(submitBookingItem.Vin2BookingDataModel.UtilityId, submitBookingItem.Vin2BookingDataModel.TimeConstraintId, submitBookingItem.Vin2BookingDataModel.PlaceId, captchaItem.Captcha, submitBookingItem.Vin2BookingDataModel.cs);
                        var responseTask = submitBookingItem.RestClient.ExecuteAsync(restRequest);
                        submitBookingItem.ResponseTask = responseTask;
                        var restResponse = await responseTask;
                        var responseTime = DateTime.Now - actualSubmitTime;
                        var content = restResponse.Content ?? "";
                        Vin2BookingRemake.TryRemove(submitBookingItem.Vin2BookingDataModel.TimeAndPlaceText, out _);
                        if (content.Contains("transactionId", StringComparison.CurrentCultureIgnoreCase) || content.Contains("Tiện ích đã hết chỗ", StringComparison.CurrentCultureIgnoreCase))
                        {
                            Vin2BookingDone.TryAdd(submitBookingItem.Vin2BookingDataModel.TimeAndPlaceText, submitBookingItem);
                        }
                        else if (content.Contains("căn hộ", StringComparison.CurrentCultureIgnoreCase))
                        {
                            Vin2BookingWrongAccountWithUtility.AddOrUpdate(submitBookingItem.Username, key => [submitBookingItem.Vin2BookingDataModel.UtilityId], (k, v) =>
                            {
                                v.Add(submitBookingItem.Vin2BookingDataModel.UtilityId);
                                return v;
                            });
                        }
                        if (restResponse.IsSuccessful)
                        {
                            _vin2Message.Info($"SubmitBooking done - {submitBookingItem.Username} - {submitBookingItem.ApartmentCode}: [{submitBookingItem.ScheduleSubmitTime:HH:mm:ss.fff}-{actualSubmitTime:HH:mm:ss.fff}--{(int)responseTime.TotalMilliseconds}] - {submitBookingItem.Vin2BookingDataModel.TimeAndPlaceText} - {content}");
                        }
                        else
                        {
                            _vin2Message.Error($"SubmitBooking fail - {submitBookingItem.Username}: [{submitBookingItem.ScheduleSubmitTime:HH:mm:ss.fff}-{actualSubmitTime:HH:mm:ss.fff}--{(int)responseTime.TotalMilliseconds}] - {submitBookingItem.Vin2BookingDataModel.TimeAndPlaceText} - {restResponse.ResponseStatus} - {restResponse.ErrorMessage ?? ""} - {content}");
                        }
                        break;
                    }
                    if ((submitBookingItem.ScheduleSubmitTime - DateTime.Now).TotalSeconds < 5)
                    {
                        _vin2Message.Error($"{submitBookingItem.Vin2BookingDataModel.TimeAndPlaceText} - Take captcha FAIL for account {submitBookingItem.Username}");
                        break;
                    }
                    await Task.Delay(200);
                }
            });
            await Task.WhenAll(executeSubmitBookingItems);
            _vin2Message.Info($"SubmitBooking ALL done");
        }

        public async void StartBookingAsync(DateTime bookingDate)
        {
            IsRunning = true;

            var takeTokenTime = Vin2BookingConfigs.TakeTokenTimeCS;
            var resolveCaptchaTime = Vin2BookingConfigs.ResolveCaptchaTimeCS;
            var submitRequestTime = Vin2BookingConfigs.SubmitRequestTimeCS;
            var submitRequestTimespan = Vin2BookingConfigs.SubmitRequestTimespan ?? 0;
            var xRequest = Vin2BookingConfigs.XRequest ?? 1;

            _vin2Message.Info($"Waiting take tokens & make booking item: {bookingDate:dd/MM/yyyy} - TakeTokenTime: {takeTokenTime:HH:mm:ss.fff} - ResolveCaptcha: {resolveCaptchaTime:HH:mm:ss.fff} - SubmitRequest: {submitRequestTime:HH:mm:ss.fff} - Span: {submitRequestTimespan} - XRequest: {xRequest}");

            while (DateTime.Now < takeTokenTime)
            {
                await Task.Delay(1000);
            }

            _vin2Message.Info($"Begin take tokens & make booking item: {bookingDate:dd/MM/yyyy} - TakeTokenTime: {takeTokenTime:HH:mm:ss.fff} - ResolveCaptcha: {resolveCaptchaTime:HH:mm:ss.fff} - SubmitRequest: {submitRequestTime:HH:mm:ss.fff} - Span: {submitRequestTimespan} - XRequest: {xRequest}");

            await _vin2BookingData.TakeDataDictionaryAsync();
            await _vin2BookingData.TakeCsDictionaryAsync();
            await _vin2BookingData.TakeDataAsync();
            await _vin2Account.TakeAccountTokensAsync(false, null, null);

            var accountArray = _vin2Account.Vin2AccountArray.Where(x => !string.IsNullOrEmpty(x.Token)).ToArray();
            var bookingArray = _vin2BookingData.Vin2BookingArray.ToArray();

            var submitBookingItems = MakeSubmitBookingItems(bookingDate, accountArray, bookingArray, submitRequestTime, submitRequestTimespan, xRequest);

            _vin2Message.Info($"Waiting resolve captcha: {bookingDate:dd/MM/yyyy} - TakeTokenTime: {takeTokenTime:HH:mm:ss.fff} - ResolveCaptcha: {resolveCaptchaTime:HH:mm:ss.fff} - SubmitRequest: {submitRequestTime:HH:mm:ss.fff} - Span: {submitRequestTimespan} - XRequest: {xRequest}");

            while (DateTime.Now < resolveCaptchaTime)
            {
                await Task.Delay(1000);
            }

            _vin2Message.Info($"Begin resolve captcha: {bookingDate:dd/MM/yyyy} - TakeTokenTime: {takeTokenTime:HH:mm:ss.fff} - ResolveCaptcha: {resolveCaptchaTime:HH:mm:ss.fff} - SubmitRequest: {submitRequestTime:HH:mm:ss.fff} - Span: {submitRequestTimespan} - XRequest: {xRequest}");

            var totalBooking = Math.Min(bookingArray.Length, accountArray.Length) * xRequest;
            TakeReCaptchaAsync(submitRequestTime, totalBooking);

            if (_configuration.GetValue<bool>("XExecute"))
            {
                await XExecuteSubmitBookingItemsAsync(submitBookingItems);
            }
            else
            {
                await ExecuteSubmitBookingItemsAsync(submitBookingItems);
            }
            IsRunning = false;
            Vin2BookingDone.Clear();
            Vin2BookingWrongAccountWithUtility.Clear();
        }

        public async void CheckBookingAsync(DateTime bookingDate)
        {
            _vin2Message.Info($"Begin CheckBooking [{bookingDate:dd/MM/yyyy}]...");
            await _vin2Account.TakeAccountTokensAsync(false, null, null);
            var tasks = _vin2Account.Vin2AccountArray.Where(x => !string.IsNullOrEmpty(x.Token)).Select(async x =>
            {
                var list = await _vin2Request.UtilityBookingListAsync(x.Token);
                if (list.IsException || !string.IsNullOrEmpty(list.error) || !list.RestResponse_IsSuccessful || list.RestResponse_StatusCode != System.Net.HttpStatusCode.OK)
                {
                    _vin2Message.Error($"CheckBooking {(list.IsException ? "exception" : "error")}: {x.Username} - {list.RestResponse_ResponseStatus} - {list.RestResponse_StatusCode} - {list.error} - {list.message}: {x.Username} {x.Password}");
                }
                return list.data?.items?.Where(x => x.StartTime.Date == bookingDate.Date)?.ToList()?.Select(xi =>
                {
                    var pl = _vin2BookingData.Vin2BookingDataDictionary.bookingPlaces.FirstOrDefault(p => p.name.Equals(xi.place, StringComparison.CurrentCultureIgnoreCase));
                    return new
                    {
                        Text = $"{xi.StartTime:HH}{pl?.shortCode ?? xi.place} ({xi.status}) - {x.ApartmentCode} - {x.Username} {x.Password}",
                        Place = pl?.shortCode ?? xi.place,
                        Time = xi.StartTime
                    };
                }) ?? new[] { new { Text = "", Place = "", Time = DateTime.Now } }.Where(x => false);
            });
            var rs = await Task.WhenAll(tasks);
            var flat = rs.SelectMany(x => x).OrderBy(x => x.Place).ThenByDescending(x => x.Time);
            flat.ToList().ForEach(x =>
            {
                _vin2Message.Success(x.Text);
            });
            _vin2Message.Info($"End CheckBooking [{bookingDate:dd/MM/yyyy}]...");
        }
    }
}
