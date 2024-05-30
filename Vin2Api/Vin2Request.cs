using Newtonsoft.Json;
using RestSharp;
using System;
using System.Net;
using System.Threading.Tasks;
using static Vin2Api.Vin2Request;

namespace Vin2Api
{
    public class Vin2Request
    {
        public class BaseResponse<T> where T : class
        {
            public BaseResponse(RestResponse restResponse)
            {
                RestResponse_IsSuccessful = restResponse.IsSuccessful;
                RestResponse_ResponseStatus = restResponse.ResponseStatus;
                RestResponse_StatusCode = restResponse.StatusCode;
                RestResponse_ErrorMessage = restResponse.ErrorMessage;
                RestResponse_Content = restResponse.Content;
                try
                {
                    var rs = RestResponse_Content.DeserializeAnonymousType(new
                    {
                        code = "",
                        error = "",
                        message = "",
                        data = (T)null
                    });
                    code = rs?.code;
                    error = rs?.error;
                    message = rs?.message;
                    data = rs?.data;
                }
                catch (Exception ex)
                {
                    IsException = true;
                    code = "Vin2-ParseResult-Exception";
                    error = "Vin2-ParseResult-Exception";
                    message = $"{ex.Message} - {ex.StackTrace} - [{restResponse.Content} - {restResponse.ErrorMessage} - {restResponse.ErrorException.Message}]";
                    data = null;
                }
            }

            public BaseResponse(Exception ex)
            {
                IsException = true;
                RestResponse_IsSuccessful = false;
                RestResponse_ResponseStatus = ResponseStatus.Error;
                RestResponse_StatusCode = HttpStatusCode.Ambiguous;
                RestResponse_ErrorMessage = ex.Message;
                RestResponse_Content = null;

                code = "Vin2-SubmitRequest-Exception";
                error = "Vin2-SubmitRequest-Exception";
                message = $"{ex.Message} - {ex.StackTrace} - [{ex.Message} - {ex.StackTrace}]";
                data = null;
            }

            public bool IsException { get; }
            public bool RestResponse_IsSuccessful { get; }
            public ResponseStatus RestResponse_ResponseStatus { get; }
            public HttpStatusCode RestResponse_StatusCode { get; }
            public string RestResponse_ErrorMessage { get; }
            public string RestResponse_Content { get; }

            public string code { get; set; }
            public string error { get; set; }
            public string message { get; set; }

            public T data { get; set; }
        }

        public string ReCaptchaKey => "6LdqIx4nAAAAAIiwGqQcu7u1tqC_a9XqhMay-JFV";
        public string ReCaptcha_CapMonsterKey => "9ec1e0a624255299ad1b5a766bb50c45";

        public int RequestTimeout => 60000;
        public string UserAgent => "Dart/3.3 (dart:io)";
        public string Vin2TokenHeader => "X-Vinhome-Token";

        public string Host => "https://vh.vinhomes.vn";
        public string LoginApi => "/api/vhr/iam/v0/security/oauth-login";
        public string MeApi => "/api/vhr/customer/v0/user/me";
        public string ChangePasswordApi => "/api/vhr/iam/v0/security/oauth-change-password";
        public string ApartmentApi => "/api/vhr/customer/v0/apartment";
        public string ApartmentSelectApi => "/api/vhr/customer/v0/apartment/select";
        public string UtilityApi => "/api/vhr/utility/v0/utility"; //list Utility: tenis, golf,...
        public string UtilityBookingTimeApi => "/api/vhr/utility/v0/utility/{0}/booking-time?bookingDate={1}"; //list time 14-15, 15-16,...
        public string UtilityPlaceApi => "/api/vhr/utility/v0/utility/{0}/places?classifyId=0&fromTime={1}&timeConstraintId={2}&monthlyTicket=false"; //list place: cv1, cv2, vs1, vs2,...
        public string UtilityTicketInfoApi => "/api/vhr/utility/v0/utility/ticket-info?bookingDate={0}&placeUtilityId={1}&timeConstraintId={2}";
        public string UtilityBookingApi => "/api/vhr/utility/v0/customer-utility/booking"; //submit booking
        public string UtilityGetTransactionApi => "/api/vhr/utility/v0/customer-utility/get-by-transaction?transactionId={0}"; //detail one booking
        public string UtilityDetailOneBookingApi => "/api/vhr/utility/v0/customer-utility/{0}"; //detail one booking
        public string UtilityBookingListApi => "/api/vhr/utility/v0/customer-utility?pageSize=10&pageNumber=0"; //list booking
        public string UtilityCancelBookingApi => "/api/vhr/utility/v0/customer-utility/{0}/cancel-booking";

        public long UtilityId_Tenis => 84;
        public long UtilityId_Golf => 85;
        public long UtilityId_SDN => 132;
        public long UtilityId_SCL => 86;

        public class LoginResponse : BaseResponse<LoginResponse._data>
        {
            public LoginResponse(RestResponse restResponse) : base(restResponse) { }
            public LoginResponse(Exception ex) : base(ex) { }
            public class _data
            {
                public class _customerInfo
                {
                    public long? id { get; set; }
                    public string fullName { get; set; }
                    public string mobile { get; set; }
                    public long? apartmentId { get; set; }
                    public string apartmentCode { get; set; }
                    public string areaName { get; set; }
                    public string apartmentStatus { get; set; }
                    public string accountingCode { get; set; }
                }
                public string accessToken { get; set; }
                public string refreshToken { get; set; }
                public string tokenType { get; set; }
                public _customerInfo customerInfo { get; set; }
            }
        }
        public async Task<LoginResponse> LoginAsync(string username, string password)
        {
            var options = new RestClientOptions(Host)
            {
                MaxTimeout = RequestTimeout,
                UserAgent = UserAgent
            };
            using var client = new RestClient(options);
            var request = new RestRequest(LoginApi, Method.Post);
            request.AddHeader("Content-Type", "application/json");
            var body = new {username, password}.SerializeObject();
            request.AddStringBody(body, DataFormat.Json);
            try
            {
                RestResponse response = await client.ExecuteAsync(request);
                return new LoginResponse(response);
            }
            catch (Exception ex)
            {
                return new LoginResponse(ex);
            }
        }

        public class MeResponse : BaseResponse<LoginResponse._data._customerInfo> 
        {
            public MeResponse(RestResponse restResponse) : base(restResponse) { }
            public MeResponse(Exception ex) : base(ex) { }
        }
        public async Task<MeResponse> MeAsync(string token)
        {
            var options = new RestClientOptions(Host)
            {
                MaxTimeout = RequestTimeout,
                UserAgent = UserAgent
            };
            using var client = new RestClient(options);
            var request = new RestRequest(MeApi, Method.Get);
            request.AddHeader("Content-Type", "application/json");
            request.AddHeader(Vin2TokenHeader, token);
            try
            {
                RestResponse response = await client.ExecuteAsync(request);
                return new MeResponse(response);
            }
            catch (Exception ex)
            {
                return new MeResponse(ex);
            }
        }

        public class ChangePasswordResponse : BaseResponse<string>
        {
            public ChangePasswordResponse(RestResponse restResponse) : base(restResponse) { }
            public ChangePasswordResponse(Exception ex) : base(ex) { }
        }
        public async Task<ChangePasswordResponse> ChangePasswordAsync(string token, string oldPassword, string newPassword)
        {
            var options = new RestClientOptions(Host)
            {
                MaxTimeout = RequestTimeout,
                UserAgent = UserAgent
            };
            using var client = new RestClient(options);
            var request = new RestRequest(ChangePasswordApi, Method.Post);
            request.AddHeader("Content-Type", "application/json");
            request.AddHeader(Vin2TokenHeader, token);
            var body = new { oldPassword, password = newPassword }.SerializeObject();
            request.AddStringBody(body, DataFormat.Json);
            try
            {
                RestResponse response = await client.ExecuteAsync(request);
                return new ChangePasswordResponse(response);
            }
            catch (Exception ex)
            {
                return new ChangePasswordResponse(ex);
            }
        }

        public class ApartmentResponse : BaseResponse<ApartmentResponse._data>
        {
            public ApartmentResponse(RestResponse restResponse) : base(restResponse) { }
            public ApartmentResponse(Exception ex) : base(ex) { }
            public class _data
            {
                public class _item
                {
                    public long? id { get; set; }
                    public string apartmentName { get; set; }
                    public string areaName { get; set; }
                    public bool? active { get; set; }
                }
                public long totalPages { get; set; }
                public long totalItems { get; set; }
                public long nbItems { get; set; }
                public long pageSize { get; set; }
                public long pageNumber { get; set; }
                public bool isFirstPage { get; set; }
                public bool isLastPage { get; set; }

                public _item[] items { get; set; }
            }
        }
        public async Task<ApartmentResponse> ApartmentAsync(string token)
        {
            var options = new RestClientOptions(Host)
            {
                MaxTimeout = RequestTimeout,
                UserAgent = UserAgent
            };
            using var client = new RestClient(options);
            var request = new RestRequest(ApartmentApi, Method.Get);
            request.AddHeader("Content-Type", "application/json");
            request.AddHeader(Vin2TokenHeader, token);
            try
            {
                RestResponse response = await client.ExecuteAsync(request);
                return new ApartmentResponse(response);
            }
            catch (Exception ex)
            {
                return new ApartmentResponse(ex);
            }
        }

        public class ApartmentSelectResponse : BaseResponse<ApartmentSelectResponse._data>
        {
            public ApartmentSelectResponse(RestResponse restResponse) : base(restResponse) { }
            public ApartmentSelectResponse(Exception ex) : base(ex) { }
            public class _data
            {
                public long? id { get; set; }
                public string apartmentCode { get; set; }
                public string areaName { get; set; }
            }
        }
        public async Task<ApartmentSelectResponse> ApartmentSelectAsync(string token, long id)
        {
            var options = new RestClientOptions(Host)
            {
                MaxTimeout = RequestTimeout,
                UserAgent = UserAgent
            };
            using var client = new RestClient(options);
            var request = new RestRequest(ApartmentSelectApi, Method.Post);
            request.AddHeader("Content-Type", "application/json");
            request.AddHeader(Vin2TokenHeader, token);
            var body = new { id }.SerializeObject();
            request.AddStringBody(body, DataFormat.Json);
            try
            {
                RestResponse response = await client.ExecuteAsync(request);
                return new ApartmentSelectResponse(response);
            }
            catch (Exception ex)
            {
                return new ApartmentSelectResponse(ex);
            }
        }

        public class UtilityResponse : BaseResponse<UtilityResponse._data[]>
        {
            public UtilityResponse(RestResponse restResponse) : base(restResponse) { }
            public UtilityResponse(Exception ex) : base(ex) { }
            public class _data
            {
                public long id { get; set; }
                public string areaName { get; set; }
                public string code { get; set; }
                public string name { get; set;}
            }
        }
        public async Task<UtilityResponse> UtilityAsync(string token)
        {
            var options = new RestClientOptions(Host)
            {
                MaxTimeout = RequestTimeout,
                UserAgent = UserAgent
            };
            using var client = new RestClient(options);
            var request = new RestRequest(UtilityApi, Method.Get);
            request.AddHeader("Content-Type", "application/json");
            request.AddHeader(Vin2TokenHeader, token);
            try
            {
                RestResponse response = await client.ExecuteAsync(request);
                return new UtilityResponse(response);
            }
            catch (Exception ex)
            {
                return new UtilityResponse(ex);
            }
        }

        public class UtilityBookingTimeResponse : BaseResponse<UtilityBookingTimeResponse._data[]>
        {
            public UtilityBookingTimeResponse(RestResponse restResponse) : base(restResponse) { }
            public UtilityBookingTimeResponse(Exception ex) : base(ex) { }
            public class _data
            {
                public long id { get; set; }
                public long utilityId { get; set; }
                public long fromTime { get; set; }
                public long toTime { get; set; }
                public bool isFull { get; set; }

                [JsonIgnore]
                public DateTime FromTime => fromTime.JsUtcToCsLocal();

                [JsonIgnore]
                public DateTime ToTime => toTime.JsUtcToCsLocal();

                [JsonIgnore]
                public string TimeRange => $"{FromTime.Hour}-{ToTime.Hour}";
            }
        }
        public async Task<UtilityBookingTimeResponse> UtilityBooingTimeAsync(string token, long utilityId, long bookingDate)
        {
            var options = new RestClientOptions(Host)
            {
                MaxTimeout = RequestTimeout,
                UserAgent = UserAgent
            };
            using var client = new RestClient(options);
            var request = new RestRequest(string.Format(UtilityBookingTimeApi, utilityId, bookingDate), Method.Get);
            request.AddHeader("Content-Type", "application/json");
            request.AddHeader(Vin2TokenHeader, token);
            try
            {
                RestResponse response = await client.ExecuteAsync(request);
                return new UtilityBookingTimeResponse(response);
            }
            catch (Exception ex)
            {
                return new UtilityBookingTimeResponse(ex);
            }
        }

        public class UtilityPlaceResponse : BaseResponse<UtilityPlaceResponse._data[]>
        {
            public UtilityPlaceResponse(RestResponse restResponse) : base(restResponse) { }
            public UtilityPlaceResponse(Exception ex) : base(ex) { }
            public class _data
            {
                public long id { get; set; }
                //Use for UtilityTicketInfo only
                public long placeUtilityId { get; set; }
                public string code { get; set; }
                public string name { get; set; }
                public bool isFull { get; set; }
            }
        }
        public async Task<UtilityPlaceResponse> UtilityPlaceAsync(string token, long utilityId, long bookingDate, long timeConstraintId)
        {
            var options = new RestClientOptions(Host)
            {
                MaxTimeout = RequestTimeout,
                UserAgent = UserAgent
            };
            using var client = new RestClient(options);
            var request = new RestRequest(string.Format(UtilityPlaceApi, utilityId, bookingDate, timeConstraintId), Method.Get);
            request.AddHeader("Content-Type", "application/json");
            request.AddHeader(Vin2TokenHeader, token);
            try
            {
                RestResponse response = await client.ExecuteAsync(request);
                return new UtilityPlaceResponse(response);
            }
            catch (Exception ex)
            {
                return new UtilityPlaceResponse(ex);
            }
        }

        public class UtilityTicketInfoResponse : BaseResponse<UtilityTicketInfoResponse._data>
        {
            public UtilityTicketInfoResponse(RestResponse restResponse) : base(restResponse) { }
            public UtilityTicketInfoResponse(Exception ex) : base(ex) { }
            public class _data
            {
            }
        }
        public async Task<UtilityTicketInfoResponse> UtilityTicketInfoAsync(string token, long bookingDate, long placeUtilityId, long timeConstraintId)
        {
            var options = new RestClientOptions(Host)
            {
                MaxTimeout = RequestTimeout,
                UserAgent = UserAgent
            };
            using var client = new RestClient(options);
            var request = new RestRequest(string.Format(UtilityTicketInfoApi, bookingDate, placeUtilityId, timeConstraintId), Method.Get);
            request.AddHeader("Content-Type", "application/json");
            request.AddHeader(Vin2TokenHeader, token);
            try
            {
                RestResponse response = await client.ExecuteAsync(request);
                return new UtilityTicketInfoResponse(response);
            }
            catch (Exception ex)
            {
                return new UtilityTicketInfoResponse(ex);
            }
        }

        public class SubmitBookingResponse : BaseResponse<SubmitBookingResponse._data>
        {
            public SubmitBookingResponse(RestResponse restResponse) : base(restResponse) { }
            public SubmitBookingResponse(Exception ex) : base(ex) { }
            public class _data
            {
                public long transactionId { get; set; }
                public long userId { get; set; }
            }
        }
        public async Task<SubmitBookingResponse> SubmitBookingAsync(string token, long utilityId, long bookingDate, long timeConstraintId, long placeId, string recaptchaResponse, string cs)
        {
            var options = new RestClientOptions(Host)
            {
                MaxTimeout = RequestTimeout,
                UserAgent = UserAgent
            };
            using var client = new RestClient(options);
            var bookingParams = MakeSubmitBookingSlot(token, bookingDate);
            var request = bookingParams.MakeSubmitBookingRequest(utilityId, timeConstraintId, placeId, recaptchaResponse, cs);
            try
            {
                RestResponse response = await client.ExecuteAsync(request);
                return new SubmitBookingResponse(response);
            }
            catch (Exception ex)
            {
                return new SubmitBookingResponse(ex);
            }
        }

        public class SubmitBookingSlot
        {
            public long BookingDate { get; internal set; }
            public string Token { get; internal set; }

            public string UtilityBookingApi { get; internal set; }
            public string Vin2TokenHeader { get; internal set; }

            public RestRequest MakeSubmitBookingRequest(long utilityId, long timeConstraintId, long placeId, string recaptchaResponse, string cs)
            {
                var request = new RestRequest(UtilityBookingApi, Method.Post);
                request.AddHeader("Content-Type", "application/json");
                request.AddHeader(Vin2TokenHeader, Token);
                var body = new
                {
                    bookingRequests = new[]
                    {
                        new
                        {
                            bookingDate = BookingDate,
                            placeId,
                            timeConstraintId,
                            utilityId,
                            residentTicket = 4,
                            residentChildTicket= (string)null,
                            guestTicket= (string)null,
                            guestChildTicket= (string)null,
                        }
                    },
                    paymentMethod = (string)null,
                    recaptchaResponse,
                    cs
                }.SerializeObject();
                request.AddStringBody(body, DataFormat.Json);
                return request;
            }
        }
        public SubmitBookingSlot MakeSubmitBookingSlot(string token, long bookingDate)
        {
            var rs = new SubmitBookingSlot
            {
                BookingDate = bookingDate,
                Token = token,

                UtilityBookingApi = UtilityBookingApi,
                Vin2TokenHeader = Vin2TokenHeader
            };
            return rs;
        }

        public class UtilityBookingListResponse : BaseResponse<UtilityBookingListResponse._data>
        {
            public UtilityBookingListResponse(RestResponse restResponse) : base(restResponse) { }
            public UtilityBookingListResponse(Exception ex) : base(ex) { }
            public class _data
            {
                public class _item
                {
                    public long id { get; set; }
                    public string name { get; set; }
                    public string place { get; set; }
                    public string status { get; set; }
                    public long bookingTime { get; set; }
                    public long startTime { get; set; }
                    public long endTime { get; set; }
                    public string utilityName { get; set; }

                    [JsonIgnore]
                    public DateTime BookingTime => bookingTime.JsUtcToCsLocal();

                    [JsonIgnore]
                    public DateTime StartTime => startTime.JsUtcToCsLocal();

                    [JsonIgnore]
                    public DateTime EndTime => endTime.JsUtcToCsLocal();
                }
                public long totalPages { get; set; }
                public long totalItems { get; set; }
                public long nbItems { get; set; }
                public long pageSize { get; set; }
                public long pageNumber { get; set; }
                public bool isFirstPage { get; set; }
                public bool isLastPage { get; set; }

                public _item[] items { get; set; }
            }
        }
        public async Task<UtilityBookingListResponse> UtilityBookingListAsync(string token)
        {
            var options = new RestClientOptions(Host)
            {
                MaxTimeout = RequestTimeout,
                UserAgent = UserAgent
            };
            using var client = new RestClient(options);
            var request = new RestRequest(UtilityBookingListApi, Method.Get);
            request.AddHeader("Content-Type", "application/json");
            request.AddHeader(Vin2TokenHeader, token);
            try
            {
                RestResponse response = await client.ExecuteAsync(request);
                return new UtilityBookingListResponse(response);
            }
            catch (Exception ex)
            {
                return new UtilityBookingListResponse(ex);
            }
        }

        public class UtilityDetailOneBookingResponse : BaseResponse<UtilityDetailOneBookingResponse._data>
        {
            public UtilityDetailOneBookingResponse(RestResponse restResponse) : base(restResponse) { }
            public UtilityDetailOneBookingResponse(Exception ex) : base(ex) { }
            public class _data
            {
                public class _qrCode 
                {
                    public string customerUtilityTicketType { get; set; }
                    public string ticketType { get; set; }
                    public string qrCode { get; set; }
                }
                public long id { get; set; }
                public long transactionId { get; set; }
                public string userName { get; set; }
                public string mobile { get; set; }
                public string apartmentCode { get; set; }
                public long createdDate { get; set; }
                public string type { get; set; }
                public string code { get; set; }
                public string name { get; set; }
                public string status { get; set; }
                public string place { get; set; }
                public long bookingTime { get; set; }
                public long startTime { get; set; }
                public long endTime { get; set; }

                [JsonIgnore]
                public DateTime CreatedDate => createdDate.JsUtcToCsLocal();

                [JsonIgnore]
                public DateTime BookingTime => bookingTime.JsUtcToCsLocal();

                [JsonIgnore]
                public DateTime StartTime => startTime.JsUtcToCsLocal();

                [JsonIgnore]
                public DateTime EndTime => endTime.JsUtcToCsLocal();

                public _qrCode[] qrCodes { get; set; }
            }
        }
        public async Task<UtilityDetailOneBookingResponse> UtilityDetailOneBookingAsync(string token, long bookingId)
        {
            var options = new RestClientOptions(Host)
            {
                MaxTimeout = RequestTimeout,
                UserAgent = UserAgent
            };
            using var client = new RestClient(options);
            var request = new RestRequest(string.Format(UtilityDetailOneBookingApi, bookingId), Method.Get);
            request.AddHeader("Content-Type", "application/json");
            request.AddHeader(Vin2TokenHeader, token);
            try
            {
                RestResponse response = await client.ExecuteAsync(request);
                return new UtilityDetailOneBookingResponse(response);
            }
            catch (Exception ex)
            {
                return new UtilityDetailOneBookingResponse(ex);
            }
        }

        public async Task<BaseResponse<string>> CancelBookingAsync(string token, long bookingId)
        {
            var options = new RestClientOptions(Host)
            {
                MaxTimeout = RequestTimeout,
                UserAgent = UserAgent
            };
            using var client = new RestClient(options);
            var request = new RestRequest(string.Format(UtilityCancelBookingApi, bookingId), Method.Post);
            request.AddHeader("Content-Type", "application/json");
            request.AddHeader(Vin2TokenHeader, token);
            try
            {
                RestResponse response = await client.ExecuteAsync(request);
                return new BaseResponse<string>(response);
            }
            catch (Exception ex)
            {
                return new BaseResponse<string>(ex);
            }
        }
    }
}
