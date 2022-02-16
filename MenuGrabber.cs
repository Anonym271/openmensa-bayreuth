using HtmlAgilityPack;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace OpenMensa_Bayreuth
{
    public enum MensaType { FRISCHRAUM, HAUPTMENSA }

    public class MenuGrabber
    {
        private const string BASE_URL = "https://www.studentenwerk-oberfranken.de/essen/speiseplaene/bayreuth/";

        private static MenuGrabber _instance = null;
        public static MenuGrabber Instance
        {
            get
            {
                if (_instance == null)
                    _instance = new();
                return _instance;
            }
        }

        private Timer _hourlyScheduler;
        private DateTime _lastWeeklyReceived;

        private HtmlDocument _todayDocH = new();
        private HtmlDocument[] _weeklyDocH = new HtmlDocument[3];
        private HtmlDocument _todayDocF = new();
        private HtmlDocument[] _weeklyDocF = new HtmlDocument[3];

        public HtmlDocument TodayHauptmensa => _todayDocH;
        public HtmlDocument[] WeeklyHauptmensa => _weeklyDocH;
        public HtmlDocument TodayFrischraum => _todayDocF;
        public HtmlDocument[] WeeklyFrischraum => _weeklyDocF;


        private MenuGrabber()
        {
            for (int i = 0;  i < 3; i++)
            {
                _weeklyDocF[i] = new();
                _weeklyDocH[i] = new();
            }
            _lastWeeklyReceived = DateTime.MinValue;
            _hourlyScheduler = new(this.Update, null, 0, 60 * 60 * 1000); // execute every hour
        }

        private async void Update(Object junk)
        {
            var dailyTask = UpdateToday();

            var now = DateTime.Now;
            if (_lastWeeklyReceived.Day != now.Day // not checked today
                && now.Hour >= 2) // past 5:00 
            {
                await UpdateWeekly();
                _lastWeeklyReceived = now;
            }

            await dailyTask;
        }

        private async Task UpdateToday()
        {
            var th = Get(MensaType.HAUPTMENSA, DateTime.Now, false);
            var tf = Get(MensaType.HAUPTMENSA, DateTime.Now, false);
            _todayDocH = await th;
            _todayDocF = await tf;
        }

        private async Task UpdateWeekly()
        {
            var tasksH = new Task<HtmlDocument>[3];
            var tasksF = new Task<HtmlDocument>[3];

            var now = DateTime.Now;
            for (int i = 0; i < 3; i++)
            {
                tasksH[i] = Get(MensaType.HAUPTMENSA, now.AddDays(i * 7), true);
                tasksF[i] = Get(MensaType.FRISCHRAUM, now.AddDays(i * 7), true);
            }

            var newH = new HtmlDocument[3];
            var newF = new HtmlDocument[3];

            for (int i = 0; i < 3; i++)
            {
                newH[i] = await tasksH[i];
                newF[i] = await tasksF[i];
            }

            _weeklyDocF = newF;
            _weeklyDocH = newH;
        }

        private static async Task<HtmlDocument> Get(MensaType mensaType, DateTime date, bool week = false)
        {
            StringBuilder urlBuilder = new(BASE_URL);
            urlBuilder.Append(mensaType switch
            {
                MensaType.FRISCHRAUM => "frischraum",
                MensaType.HAUPTMENSA => "hauptmensa",
                _ => throw new ArgumentException("Not a valid MensaType", nameof(mensaType)),
            })
                .Append('/')
                .Append(week ? "woche" : "tag")
                .Append('/')
                .Append(date.ToString("yyyy-MM-dd"))
                .Append(".html");

            var web = new HtmlWeb();
            return await web.LoadFromWebAsync(urlBuilder.ToString());
        }

        public HtmlDocument GetToday(MensaType mensa) => mensa switch
        {
            MensaType.HAUPTMENSA => TodayHauptmensa,
            MensaType.FRISCHRAUM => TodayFrischraum,
            _ => throw new ArgumentException("Illegal Mensa " + mensa.ToString())
        };
        public HtmlDocument[] GetWeekly(MensaType mensa) => mensa switch
        {
            MensaType.HAUPTMENSA => WeeklyHauptmensa,
            MensaType.FRISCHRAUM => WeeklyFrischraum,
            _ => throw new ArgumentException("Illegal Mensa " + mensa.ToString())
        };
    }
}
