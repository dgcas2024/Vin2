using System.Linq;
using System;
using Vin2Api;

namespace Vin2
{
    public class Vin2Messenger
    {
        private readonly Lazy<Vin2Account> _vin2Account;
        private readonly Lazy<Vin2BookingData> _vin2BookingData;
        private readonly Lazy<Vin2Booking> _vin2Booking;
        private readonly Lazy<IVin2Message> _vin2Message;

        public Vin2Messenger(Lazy<Vin2Account> vin2Account, Lazy<Vin2BookingData> vin2BookingData, Lazy<Vin2Booking> vin2Booking, Lazy<IVin2Message> vin2Message)
        {
            _vin2Account = vin2Account;
            _vin2BookingData = vin2BookingData;
            _vin2Booking = vin2Booking;
            _vin2Message = vin2Message;
        }

        public void NotifyUpdateBookingInfo()
        {
            var totalAccount = _vin2Account.Value.Vin2AccountArray.Count;
            var totalAccountTotken = _vin2Account.Value.Vin2AccountArray.Count(x => !string.IsNullOrEmpty(x.Token));
            var bookingArray = _vin2BookingData.Value.Vin2BookingArray.Count;
            _vin2Message.Value.SendObject(new
            {
                Type = "UpdateBookingInfo",
                TotalAccount = totalAccount,
                TotalAccountToken = totalAccountTotken,
                BookingArray = bookingArray,
                TotalBooking = Math.Min(totalAccountTotken, bookingArray) * (_vin2Booking.Value.Vin2BookingConfigs?.XRequest ?? 1)
            });
        }
    }
}
