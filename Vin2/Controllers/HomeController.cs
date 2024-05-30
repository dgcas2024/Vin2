using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using System;
using System.Threading.Tasks;
using Vin2Api;

namespace Vin2.Controllers
{
    public class HomeController : Controller
    {
        private readonly ILogger<HomeController> _logger;
        private readonly Vin2Account _vin2Account;
        private readonly Vin2Request _vin2Request;
        private readonly Vin2BookingData _vin2BookingData;
        private readonly Vin2Booking _vin2Booking;

        public HomeController(ILogger<HomeController> logger, Vin2Account vin2Account, Vin2Request vin2Request, Vin2BookingData vin2BookingData, Vin2Booking vin2Booking)
        {
            _logger = logger;
            _vin2Account = vin2Account;
            _vin2Request = vin2Request;
            _vin2BookingData = vin2BookingData;
            _vin2Booking = vin2Booking;
        }

        public async Task<IActionResult> Index()
        {
            //var token = await _vin2Request.LoginAsync("0837777494", "Tttt1111@");
            //var token = "eyJhbGciOiJIUzUxMiJ9.eyJqdGkiOiI0MTczMSIsInN1YiI6IjA4Mzc3Nzc0OTQiLCJhdWQiOiJvYXV0aCIsImlhdCI6MTcwNzAzMTEwOCwiZXhwIjoxNzEzMDc5MTA4fQ.lmlmZe0Lxrc3JQlo43jHMqEZcEeenBiMcrgbjHcQrzX9Ce15MnWFECt2DfqfAUXbKmluZ3bzd1PP0Mo_Qex22g";
            //var bookingDate = DateTime.Now.Date.AddDays(2).CsLocalToJsUtc();
            //var bookingTimeTenis = await _vin2Request.UtilityBooingTimeAsync(token, _vin2Request.UtilityId_Tenis, bookingDate);
            //var bookingTimeSdn = await _vin2Request.UtilityBooingTimeAsync(token, _vin2Request.UtilityId_SDN, bookingDate);
            //var bookingTimeScl = await _vin2Request.UtilityBooingTimeAsync(token, _vin2Request.UtilityId_SCL, bookingDate);
            //var bookingTimes = bookingTimeTenis.data.Select(x => new { x.id, x.utilityId, x.TimeRange, utilityName = "Tenis" })
            //    .Concat(bookingTimeSdn.data.Select(x => new { x.id, x.utilityId, x.TimeRange, utilityName = "SDN" }))
            //    .Concat(bookingTimeScl.data.Select(x => new { x.id, x.utilityId, x.TimeRange, utilityName = "SCL" }));
            //var fromTime = bookingTimeTenis.data[0].fromTime;
            //var bookingPlaceTenis = await _vin2Request.UtilityPlaceAsync(token, _vin2Request.UtilityId_Tenis, bookingTimeTenis.data[0].fromTime, bookingTimeTenis.data[0].id);
            //var bookingPlaceSdn = await _vin2Request.UtilityPlaceAsync(token, _vin2Request.UtilityId_SDN, bookingTimeSdn.data[0].fromTime, bookingTimeSdn.data[0].id);
            //var bookingPlaceScl = await _vin2Request.UtilityPlaceAsync(token, _vin2Request.UtilityId_SCL, bookingTimeScl.data[0].fromTime, bookingTimeScl.data[0].id);
            //var bookingPlaces = bookingPlaceTenis.data.Select(x => new { x.id, x.name })
            //    .Concat(bookingPlaceSdn.data.Select(x => new { x.id, x.name }))
            //    .Concat(bookingPlaceScl.data.Select(x => new { x.id, x.name }));
            //var bookingData = new
            //{
            //    bookingTimes,
            //    bookingPlaces
            //};
            //var json = Newtonsoft.Json.JsonConvert.SerializeObject(bookingData, Newtonsoft.Json.Formatting.Indented);
            //System.IO.File.WriteAllText("booking-data-dictionary", json);
            await Task.CompletedTask;
            return View();
        }

        public class ConfigsModel
        {
            public string AccountRaw { get; set; }
            public string BookingRaw { get; set; }

            public string BookingConfigs { get; set; }
        }

        [HttpPost]
        public async Task<IActionResult> SaveConfigs([FromBody]ConfigsModel model)
        {
            if (_vin2Booking.IsRunning)
            {
                return Json(new
                {
                    Success = false,
                    Message = "Failed [Vin2Booking.IsRunning]"
                });
            }
            await _vin2Account.Save(model.AccountRaw);
            await _vin2BookingData.Save(model.BookingRaw);
            await _vin2Booking.Save(model.BookingConfigs);
            return Json(new
            {
                Success = true
            });
        }

        [HttpPost]
        public IActionResult StartBooking()
        {
            if (_vin2Booking.IsRunning)
            {
                return Json(new
                {
                    Success = false,
                    Message = "Failed [Vin2Booking.IsRunning]"
                });
            }
            _vin2Booking.StartBookingAsync(DateTime.Now.Date.AddDays(1));
            return Json(new
            {
                Success = true
            });
        }

        [HttpPost]
        public IActionResult TakeAccountTokens(bool countApartment, long? availableWithUtilityId)
        {
            if (_vin2Booking.IsRunning)
            {
                return Json(new
                {
                    Success = false,
                    Message = "Failed [Vin2Booking.IsRunning]"
                });
            }
            _ = _vin2Account.TakeAccountTokensAsync(countApartment, availableWithUtilityId, availableWithUtilityId.HasValue ? DateTime.Now.Date.AddDays(1).CsLocalToJsUtc() : null);
            return Json(new
            {
                Success = true
            });
        }

        [HttpGet]
        [ResponseCache(NoStore = true, Duration = 0)]
        public IActionResult CheckBooking(int delta)
        {
            if (_vin2Booking.IsRunning)
            {
                return Json(new
                {
                    Success = false,
                    Message = "Failed [Vin2Booking.IsRunning]"
                });
            }
            _vin2Booking.CheckBookingAsync(DateTime.Now.Date.AddDays(delta));
            return Json(new
            {
                Success = true
            });
        }

        [HttpGet]
        [ResponseCache(NoStore = true, Duration = 0)]
        public IActionResult ChangePassword(string password)
        {
            if (_vin2Booking.IsRunning)
            {
                return Json(new
                {
                    Success = false,
                    Message = "Failed [Vin2Booking.IsRunning]"
                });
            }
            _vin2Account.ChangePasswordAsync(password);
            return Json(new
            {
                Success = true
            });
        }
    }
}
