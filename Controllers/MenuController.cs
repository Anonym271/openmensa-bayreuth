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
            try
            {
                var res = new OpenMensa(await MenuParser.GetCanteenWeeks(ParseMensaType(mensaType), DateTime.Now, 3));
                _logger.LogInformation("Successfully parsed complete menu from mensa '{mensaType}'", mensaType);
                return res;
            }
            catch (ArgumentException exc)
            {
                _logger.LogError(exc, "Error while parsing all information from requested mensa '{mensaType}'", mensaType);
                throw;
            }
            catch (Exception exc)
            {
                _logger.LogError(exc, "Error parsing all information from requested mensa '{mensaType}'", mensaType);
                throw new InvalidDataException("An error occurred while parsing the requested information! See inner exception for details.", exc);
            }
        }

        [HttpGet("{mensaType}/today")]
        public async Task<OpenMensa> GetToday(string mensaType)
        {
            try
            {
                var res = new OpenMensa(await MenuParser.GetCanteenToday(ParseMensaType(mensaType)));
                _logger.LogInformation("Successfully parsed today's menu from mensa '{mensaType}'", mensaType);
                return res;
            }
            catch (ArgumentException exc)
            {
                _logger.LogError(exc, "Error while parsing today's information from requested mensa '{mensaType}'", mensaType);
                throw;
            }
            catch (Exception exc)
            {
                _logger.LogError(exc, "Error while parsing today's information from requested mensa '{mensaType}'", mensaType);
                throw new InvalidDataException("An error occurred while parsing the requested information! See inner exception for details.", exc);
            }
        }
    }
}
