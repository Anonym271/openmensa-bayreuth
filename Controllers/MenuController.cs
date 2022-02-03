using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using System.Xml.Serialization;

namespace OpenMensa_Bayreuth.Controllers
{
    [ApiController]
    [Route("mensa")]
    [Produces("application/rss+xml")]
    public class MenuController : ControllerBase
    {
        private readonly ILogger<MenuController> _logger;

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

        [HttpGet("{mensaType}")]
        public async Task<OpenMensa> GetAll(string mensaType)
        {
            return new OpenMensa(await MenuParser.GetCanteenWeeks(ParseMensaType(mensaType), DateTime.Now, 3));
        }

        [HttpGet("{mensaType}/today")]
        public async Task<OpenMensa> GetToday(string mensaType)
        {
            return new OpenMensa(await MenuParser.GetCanteenToday(ParseMensaType(mensaType)));
        }
    }
}
