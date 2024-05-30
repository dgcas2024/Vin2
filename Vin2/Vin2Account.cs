using Microsoft.Extensions.Hosting;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Vin2Api;

namespace Vin2
{
    public class Vin2AccountModel
    {
        public string Username { get; set; }
        public string Password { get; set; }
        public string Token { get; set; }
        public string ApartmentCode { get; set; }
    }

    public class Vin2Account
    {
        private static readonly Regex _removeDuplicateSpace = new("\\s\\s+", RegexOptions.Compiled);

        private readonly IHostEnvironment _hostEnvironment;
        private readonly Vin2Request _vin2Request;
        private readonly IVin2Message _vin2Message;
        private readonly Vin2Messenger _vin2Messenger;

        public List<Vin2AccountModel> Vin2AccountArray { get; } = [];
        public string Vin2AccountRaw { get; private set; }

        public Vin2Account(IHostEnvironment hostEnvironment, Vin2Request vin2Request, IVin2Message vin2Message, Vin2Messenger vin2Messenger) 
        {
            _hostEnvironment = hostEnvironment;
            _vin2Request = vin2Request;
            _vin2Message = vin2Message;
            _vin2Messenger = vin2Messenger;

            _ = TakeAccountsAsync();
        }

        public async void ChangePasswordAsync(string password)
        {
            await TakeAccountTokensAsync(false, null, null);
            var tasks = Vin2AccountArray.Where(x => x.Token != null && x.Password != password).Select(async x =>
            {
                if (x.Password != password)
                {
                    var rs = await _vin2Request.ChangePasswordAsync(x.Token, x.Password, password);
                    if (rs.IsException || !string.IsNullOrEmpty(rs.error) || !rs.RestResponse_IsSuccessful || rs.RestResponse_StatusCode != System.Net.HttpStatusCode.OK)
                    {
                        _vin2Message.Error($"ChangePasswordAsync {(rs.IsException ? "exception" : "error")}: {rs.RestResponse_ResponseStatus} - {rs.RestResponse_StatusCode} - {rs.error} - {rs.message}: {x.Username} {x.Password} -> {password}");
                    }
                    else
                    {
                        _vin2Message.Success($"ChangePasswordDone: {x.Username} {x.Password} -> {password}");
                    }
                }
            });
            _vin2Message.Info($"Begin ChangePassword");
            await Task.WhenAll(tasks);
            _vin2Message.Info($"ChangePassword done");
        }

        public async Task Save(string accountRaw)
        {
            await System.IO.File.WriteAllTextAsync($"{_hostEnvironment.EnvironmentName}--accounts.txt", accountRaw);
            Vin2AccountRaw = accountRaw;
            await TakeAccountsAsync();
            _vin2Messenger.NotifyUpdateBookingInfo();
        }

        public async Task TakeAccountsAsync()
        {
            Vin2AccountArray.RemoveAll(x => true);
            var accountRaw = await System.IO.File.ReadAllTextAsync($"{_hostEnvironment.EnvironmentName}--accounts.txt");
            Vin2AccountRaw = accountRaw;
            var accounts = accountRaw.Split('\r', '\n').Where(x => x != null && x.Trim().Split(' ').Length == 2).Select(x => new Vin2AccountModel
            {
                Username = x.Split(' ')[0],
                Password = x.Split(' ')[1],
                Token = null
            }).GroupBy(k => k.Username, g => g, (k, g) => g.First());
            Vin2AccountArray.AddRange(accounts);
        }

        private static string PadRight(string str, int length, char c)
        {
            return _removeDuplicateSpace.Replace(str ?? "", " ").Trim().PadRight(length, c);
        }

        public async Task TakeAccountTokensAsync(bool countApartment, long? availableWithUtilityId, long? bookingDate)
        {
            _vin2Message.Info($"Begin TakeAccountTokens: {Vin2AccountArray.Count}");
            Vin2AccountArray.ForEach(x =>
            {
                x.Token = null;
                x.ApartmentCode = null;
            });
            _vin2Messenger.NotifyUpdateBookingInfo();
            var tasks = Vin2AccountArray.Select(x => Task.Run(async () =>
            {
                var login = await _vin2Request.LoginAsync(x.Username, x.Password);
                if (login.IsException || !string.IsNullOrEmpty(login.error) || !login.RestResponse_IsSuccessful || login.RestResponse_StatusCode != System.Net.HttpStatusCode.OK)
                {
                    _vin2Message.Error($"LoginAsync {(login.IsException ? "exception" : "error")}: {login.RestResponse_ResponseStatus} - {login.RestResponse_StatusCode} - {login.error} - {login.message}: {x.Username} {x.Password}");
                    return new KeyValuePair<string, string>("", "");
                }
                if (login.data.customerInfo?.id == null || login.data.customerInfo?.areaName.Contains("Central Park", StringComparison.CurrentCultureIgnoreCase) == false)
                {
                    var apartment = await _vin2Request.ApartmentAsync(login.data.accessToken);
                    if (apartment.IsException || !string.IsNullOrEmpty(apartment.error) || !apartment.RestResponse_IsSuccessful || apartment.RestResponse_StatusCode != System.Net.HttpStatusCode.OK)
                    {
                        _vin2Message.Error($"ApartmentAsync {(apartment.IsException ? "exception" : "error")}: {apartment.RestResponse_ResponseStatus} - {apartment.RestResponse_StatusCode} - {apartment.error} - {apartment.message}: {x.Username} {x.Password}");
                        return new KeyValuePair<string, string>("", "");
                    }
                    var cp = apartment.data.items?.FirstOrDefault(x => x.areaName.Contains("Central Park", StringComparison.CurrentCultureIgnoreCase));
                    Vin2Request.MeResponse me = null;
                    if (cp != null)
                    {
                        var aSelect = await _vin2Request.ApartmentSelectAsync(login.data.accessToken, cp.id.Value);
                        if (aSelect.IsException || !string.IsNullOrEmpty(aSelect.error) || !aSelect.RestResponse_IsSuccessful || aSelect.RestResponse_StatusCode != System.Net.HttpStatusCode.OK)
                        {
                            _vin2Message.Error($"ApartmentSelectAsync {(aSelect.IsException ? "exception" : "error")}: {aSelect.RestResponse_ResponseStatus} - {aSelect.RestResponse_StatusCode} - {aSelect.error} - {aSelect.message}: {x.Username} {x.Password}");
                            return new KeyValuePair<string, string>("", "");
                        }
                        me = await _vin2Request.MeAsync(login.data.accessToken);
                        if (me.IsException || !string.IsNullOrEmpty(me.error) || !me.RestResponse_IsSuccessful || me.RestResponse_StatusCode != System.Net.HttpStatusCode.OK)
                        {
                            _vin2Message.Error($"MeAsync {(me.IsException ? "exception" : "error")}: {me.RestResponse_ResponseStatus} - {me.RestResponse_StatusCode} - {me.error} - {me.message}: {x.Username} {x.Password}");
                            return new KeyValuePair<string, string>("", "");
                        }
                    }
                    if (me?.data?.id == null)
                    {
                        _vin2Message.Error($"CustomerInfo null: {x.Username} {x.Password}");
                        return new KeyValuePair<string, string>("", "");
                    }
                    login.data.customerInfo = me.data;
                }

                if (availableWithUtilityId.HasValue)
                {
                    var utilities = await _vin2Request.UtilityAsync(login.data.accessToken);
                    if (utilities.IsException || !string.IsNullOrEmpty(utilities.error) || !utilities.RestResponse_IsSuccessful || utilities.RestResponse_StatusCode != System.Net.HttpStatusCode.OK)
                    {
                        _vin2Message.Error($"UtilityAsync {(utilities.IsException ? "exception" : "error")}: {utilities.RestResponse_ResponseStatus} - {utilities.RestResponse_StatusCode} - {utilities.error} - {utilities.message}: {x.Username} {x.Password}");
                        return new KeyValuePair<string, string>("", "");
                    }
                    var apartment = await _vin2Request.ApartmentAsync(login.data.accessToken);
                    if (apartment.IsException || !string.IsNullOrEmpty(apartment.error) || !apartment.RestResponse_IsSuccessful || apartment.RestResponse_StatusCode != System.Net.HttpStatusCode.OK)
                    {
                        _vin2Message.Error($"ApartmentAsync {(apartment.IsException ? "exception" : "error")}: {apartment.RestResponse_ResponseStatus} - {apartment.RestResponse_StatusCode} - {apartment.error} - {apartment.message}: {x.Username} {x.Password}");
                        return new KeyValuePair<string, string>("", "");
                    }
                    var cpCount = apartment.data.items?.Count(x => x.areaName.Contains("Central Park", StringComparison.CurrentCultureIgnoreCase)) ?? 0;
                    if (utilities.data.Any(x => x.id == availableWithUtilityId.Value))
                    {
                        var times = await _vin2Request.UtilityBooingTimeAsync(login.data.accessToken, availableWithUtilityId.Value, bookingDate.Value);
                        if (times.IsException || !string.IsNullOrEmpty(times.error) || !times.RestResponse_IsSuccessful || times.RestResponse_StatusCode != System.Net.HttpStatusCode.OK)
                        {
                            _vin2Message.Error($"UtilityBooingTimeAsync {(times.IsException ? "exception" : "error")}: {times.RestResponse_ResponseStatus} - {times.RestResponse_StatusCode} - {times.error} - {times.message}: {x.Username} {x.Password}");
                            return new KeyValuePair<string, string>("", "");
                        }
                        var timeOne = times.data.OrderBy(x => x.isFull).FirstOrDefault();
                        if (timeOne != null)
                        {
                            var places = await _vin2Request.UtilityPlaceAsync(login.data.accessToken, availableWithUtilityId.Value, bookingDate.Value, timeOne.id);
                            if (places.IsException || !string.IsNullOrEmpty(places.error) || !places.RestResponse_IsSuccessful || places.RestResponse_StatusCode != System.Net.HttpStatusCode.OK)
                            {
                                _vin2Message.Error($"UtilityPlaceAsync {(places.IsException ? "exception" : "error")}: {places.RestResponse_ResponseStatus} - {places.RestResponse_StatusCode} - {places.error} - {places.message}: {x.Username} {x.Password}");
                                return new KeyValuePair<string, string>("", "");
                            }
                            var placeOne = places.data.OrderBy(x => x.isFull).FirstOrDefault();
                            if (placeOne != null)
                            {
                                var ticket = await _vin2Request.UtilityTicketInfoAsync(login.data.accessToken, bookingDate.Value, placeOne.placeUtilityId, timeOne.id);
                                if (ticket.IsException || !string.IsNullOrEmpty(ticket.error) || !ticket.RestResponse_IsSuccessful || ticket.RestResponse_StatusCode != System.Net.HttpStatusCode.OK)
                                {
                                    _vin2Message.Error($"UtilityTicketInfoAsync {(ticket.IsException ? "exception" : "error")}: {ticket.RestResponse_ResponseStatus} - {ticket.RestResponse_StatusCode} - {ticket.error} - {ticket.message}: {x.Username} {x.Password}");
                                    return new KeyValuePair<string, string>("", "");
                                }
                                if (ticket.data == null)
                                {
                                    _vin2Message.Error($"{cpCount.ToString().PadLeft(2, '0')} - Ticket not valid - {ticket.message ?? ticket.RestResponse_Content}: {x.Username} {x.Password}");
                                    return new KeyValuePair<string, string>("", "");
                                }
                            }
                            else
                            {
                                _vin2Message.Error($"{cpCount.ToString().PadLeft(2, '0')} - Places not valid - {places.message ?? places.RestResponse_Content}: {x.Username} {x.Password}");
                                return new KeyValuePair<string, string>("", "");
                            }
                        }
                        else
                        {
                            _vin2Message.Error($"{cpCount.ToString().PadLeft(2, '0')} - Times not valid - {times.message ?? times.RestResponse_Content}: {x.Username} {x.Password}");
                            return new KeyValuePair<string, string>("", "");
                        }
                    }
                    else
                    {
                        _vin2Message.Error($"{cpCount.ToString().PadLeft(2, '0')} - Utilities not valid - {utilities.message ?? utilities.RestResponse_Content}: {x.Username} {x.Password}");
                        return new KeyValuePair<string, string>("", "");
                    }
                }
                x.Token = login.data.accessToken;
                x.ApartmentCode = PadRight(login.data.customerInfo?.apartmentCode, 13, '-');
                var message = $"{PadRight(login.data.customerInfo?.areaName, 21, '-')} - {PadRight(login.data.customerInfo?.apartmentCode, 13, '-')} - {PadRight(login.data.customerInfo?.fullName, 25, '-')}: {x.Username} {x.Password}";
                _vin2Message.Success(message);
                _vin2Messenger.NotifyUpdateBookingInfo();
                if (countApartment)
                {
                    var apartment = await _vin2Request.ApartmentAsync(x.Token);
                    if (apartment.IsException || !string.IsNullOrEmpty(apartment.error) || !apartment.RestResponse_IsSuccessful || apartment.RestResponse_StatusCode != System.Net.HttpStatusCode.OK)
                    {
                        _vin2Message.Error($"ApartmentAsync {(apartment.IsException ? "exception" : "error")}: {apartment.RestResponse_ResponseStatus} - {apartment.RestResponse_StatusCode} - {apartment.error} - {apartment.message}: {x.Username} {x.Password}");
                        return new KeyValuePair<string, string>("", "");
                    }
                    return new KeyValuePair<string, string>(PadRight(login.data.customerInfo?.apartmentCode, 13, '-'), $"{apartment.data.totalItems.ToString().PadLeft(2, '0')} - {message}");
                }
                return new KeyValuePair<string, string>(PadRight(login.data.customerInfo?.apartmentCode, 13, '-'), message);
            }));
            var rs = await Task.WhenAll(tasks);
            _vin2Message.Info($"End TakeAccountTokens: {rs.Count(x => !string.IsNullOrEmpty(x.Key))}/{rs.Length}");
            if (countApartment)
            {
                _vin2Message.Info($"--AccountTokens with apartment info:");
                var sortItems = rs.Where(x => !string.IsNullOrEmpty(x.Key)).GroupBy(k => k.Key, g => g.Value, (k, g) => new
                {
                    ApartmentCode = k,
                    Duplicated = g.Count(),
                    Texts = g.ToArray()
                }).SelectMany(x => x.Texts.Select(xi => new
                {
                    x.ApartmentCode,
                    x.Duplicated,
                    Text = xi
                })).OrderBy(x => x.Duplicated).ThenBy(x => x.ApartmentCode).ThenBy(x => x.Text);
                foreach (var item  in sortItems)
                {
                    _vin2Message.Success(item.Text);
                }
                _vin2Message.Info($"--AccountTokens with apartment info: {rs.Count(x => !string.IsNullOrEmpty(x.Key))}/{rs.Length}");
            }
            _vin2Messenger.NotifyUpdateBookingInfo();
        }
    }
}
