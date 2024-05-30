using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Vin2Api;

namespace Vin2
{
    public class Vin2BookingDataModel
    {
        public Vin2BookingDataModel(IVin2Message vin2Message, Vin2BookingDataDictionary vin2BookingDataDictionary, Vin2BookingCsDictionary vin2BookingCsDictionary, long utilityId, long timeConstraintId, long placeId)
        {
            UtilityId = utilityId;
            TimeConstraintId = timeConstraintId;
            PlaceId = placeId;
            var time = vin2BookingDataDictionary.bookingTimes.FirstOrDefault(x => x.id == TimeConstraintId)?.TimeRange ?? "00-00";
            var place = vin2BookingDataDictionary.bookingPlaces.FirstOrDefault(x => x.id == PlaceId)?.shortCode ?? "N/A";
            cs = vin2BookingCsDictionary.CsArray.FirstOrDefault(x => x.placeId == placeId && x.time == timeConstraintId)?.cs ?? null;
            TimeAndPlaceText = $"{time}{place}";
            if (cs == null)
            {
                vin2Message.Error($"cs null: {TimeAndPlaceText}");
            }
            vin2Message.Info($"Booking time: {TimeAndPlaceText}, CS: {cs ?? ""}");
        }

        public long UtilityId { get; }
        public long TimeConstraintId { get; }
        public long PlaceId { get; }
        public string cs { get; set; }

        public string TimeAndPlaceText { get; }
    }

    public class Vin2BookingDataDictionary
    {
        public class BookingTime
        {
            public long id { get; set; }
            public long utilityId { get; set; }
            public string TimeRange { get; set; }
            public string utilityName { get; set; }
        }
        public class BookingPlace
        {
            public long id { get; set; }
            public string name { get; set; }
            public string shortCode { get; set; }
        }

        public BookingTime[] bookingTimes { get; set; }
        public BookingPlace[] bookingPlaces { get; set; }
    }

    public class Vin2BookingCsDictionary
    {
        public class Cs 
        {
            public long placeId { get; set; }
            public long time { get; set; }
            public string cs { get; set; }
        }
        public Cs[] CsArray { get; set; }
    }

    public class Vin2BookingData
    {
        private static readonly Regex _bookingRegex = new("(?<f>\\d+)-(?<t>\\d+)\\s*(?<type>cv1|cv2|vs1|vs2|scl|sdn)", RegexOptions.Singleline | RegexOptions.Compiled);

        private readonly IHostEnvironment _hostEnvironment;
        private readonly Vin2Request _vin2Request;
        private readonly IVin2Message _vin2Message;
        private readonly Vin2Messenger _vin2Messenger;
        private readonly ILogger<Vin2BookingData> _logger;

        public Vin2BookingDataDictionary Vin2BookingDataDictionary { get; private set; }
        public Vin2BookingCsDictionary Vin2BookingCsDictionary { get; private set; }

        public List<Vin2BookingDataModel> Vin2BookingArray { get; } = [];
        public string Vin2BookingDataRaw { get; private set; }

        public Vin2BookingData(IHostEnvironment hostEnvironment, Vin2Request vin2Request, IVin2Message vin2Message, Vin2Messenger vin2Messenger, ILogger<Vin2BookingData> logger)
        {
            _hostEnvironment = hostEnvironment;
            _vin2Request = vin2Request;
            _vin2Message = vin2Message;
            _vin2Messenger = vin2Messenger;
            _logger = logger;

            Task.Run(async () =>
            {
                await TakeDataDictionaryAsync();
                await TakeCsDictionaryAsync();
                await TakeDataAsync();
            });
        }

        public async Task Save(string bookingDataRaw)
        {
            await System.IO.File.WriteAllTextAsync($"{_hostEnvironment.EnvironmentName}--booking-data.txt", bookingDataRaw);
            Vin2BookingDataRaw = bookingDataRaw;
            await TakeDataAsync();
            _vin2Messenger.NotifyUpdateBookingInfo();
        }

        public async Task TakeDataDictionaryAsync()
        {
            var executingAssembly = Assembly.GetExecutingAssembly();
            var name = executingAssembly.GetName().Name;
            using var stream = executingAssembly.GetManifestResourceStream($"{name}.booking-data-dictionary.json");
            using var streamReader = new StreamReader(stream, Encoding.UTF8);
            var jsonRaw = await streamReader.ReadToEndAsync();
            Vin2BookingDataDictionary = jsonRaw.DeserializeObject<Vin2BookingDataDictionary>() ?? new Vin2BookingDataDictionary { bookingTimes = [], bookingPlaces = [] };
            _logger.LogInformation($"Vin2BookingDictionary: P/T: {Vin2BookingDataDictionary.bookingPlaces.Length}/{Vin2BookingDataDictionary.bookingTimes.Length}");
        }

        public async Task TakeCsDictionaryAsync()
        {
            var jsonRaw = await File.ReadAllTextAsync("cs-data-dictionary.json");
            Vin2BookingCsDictionary = jsonRaw.DeserializeObject<Vin2BookingCsDictionary>() ?? new Vin2BookingCsDictionary { CsArray = [] };
            _logger.LogInformation($"Vin2BookingCsDictionary: {Vin2BookingCsDictionary.CsArray.Length}");
        }

        public async Task TakeDataAsync()
        {
            Vin2BookingArray.RemoveAll(x => true);
            var bookingRaw = await System.IO.File.ReadAllTextAsync($"{_hostEnvironment.EnvironmentName}--booking-data.txt");
            Vin2BookingDataRaw = bookingRaw;
            var matches = _bookingRegex.Matches(bookingRaw ?? "");
            foreach (Match match in matches)
            {
                var from = int.Parse(match.Groups["f"].Value);
                var to = int.Parse(match.Groups["t"].Value);
                var type = match.Groups["type"].Value;
                for (var time = from; time < to; time++)
                {
                    var timeRange = $"{time}-{time + 1}";
                    var utilityId = type == "cv1" || type == "cv2" || type == "vs1" || type == "vs2" ? _vin2Request.UtilityId_Tenis :
                            type == "scl" ? _vin2Request.UtilityId_SCL : _vin2Request.UtilityId_SDN;
                    var timeConstraintId = Vin2BookingDataDictionary.bookingTimes.FirstOrDefault(x => x.utilityId == utilityId && x.TimeRange == timeRange)?.id;
                    var placeId = Vin2BookingDataDictionary.bookingPlaces.FirstOrDefault(x => x.shortCode == type)?.id;
                    if (timeConstraintId.HasValue && placeId.HasValue)
                    {
                        Vin2BookingArray.Add(new Vin2BookingDataModel(_vin2Message, Vin2BookingDataDictionary, Vin2BookingCsDictionary, utilityId, timeConstraintId.Value, placeId.Value));
                    }
                    else
                    {
                        _vin2Message.Warn($"{timeRange}{type} is not valid");
                    }
                }
            }
        }
    }
}
