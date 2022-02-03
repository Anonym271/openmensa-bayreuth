using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Xml.Serialization;

namespace OpenMensa_Bayreuth.Controllers
{
    [ApiController]
    [Route("mensa")]
    public class MenuController : ControllerBase
    {
        private readonly ILogger<MenuController> _logger;

        private static async Task<string> SerializeCanteen(Canteen canteen)
        {
            var mensa = new OpenMensa(canteen);
            XmlSerializer xml = new XmlSerializer(typeof(OpenMensa));
            using MemoryStream mem = new();
            xml.Serialize(mem, mensa);
            mem.Position = 0;
            using StreamReader reader = new(mem);
            return await reader.ReadToEndAsync();
        }

        private static MensaType ParseMensaType(string mensaString) => mensaString switch
        {
            "hauptmensa" => MensaType.HAUPTMENSA,
            "frischraum" => MensaType.FRISCHRAUM,
            _ => throw new ArgumentException("Illegal mensa type!")
        };

        public MenuController(ILogger<MenuController> logger)
        {
            _logger = logger;
        }

        [HttpGet("{mensaType}/today")]
        public async Task<string> GetToday(string mensaType)
        {
            return await SerializeCanteen(await MenuParser.GetCanteenToday(ParseMensaType(mensaType)));
        }

        [HttpGet("{mensaType}")]
        public async Task<string> GetAll(string mensaType)
        {
            return await SerializeCanteen(await MenuParser.GetCanteenWeeks(ParseMensaType(mensaType), DateTime.Now, 3));
        }
    }
}
