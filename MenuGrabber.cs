using HtmlAgilityPack;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace OpenMensa_Bayreuth
{
    public enum MensaType { FRISCHRAUM, HAUPTMENSA }

    public static class MenuGrabber
    {
        private const string BASE_URL = "https://www.studentenwerk-oberfranken.de/essen/speiseplaene/bayreuth/";

        public static async Task<HtmlDocument> Get(MensaType mensaType, DateTime date, bool week = false)
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
    }
}
