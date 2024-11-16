using Client.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Microsoft.ServiceFabric.Services.Remoting.Client;
using System;
using Interfaces;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.ServiceFabric.Services.Remoting.V2.FabricTransport.Client;
using Models;
using Microsoft.ServiceFabric.Services.Remoting.FabricTransport;

namespace Client.Controllers
{
    public class HomeController : Controller
    {
        private readonly IValidation _validationService;

        public HomeController()
        {
            //var proxyFactory = new ServiceProxyFactory(c => new FabricTransportServiceRemotingClientFactory());
            //_validationService = proxyFactory.CreateServiceProxy<IValidation>(
            //    new Uri("fabric:/Zadatak/ValidationService"));

            var serviceProxyFactory = new ServiceProxyFactory((callbackClient) =>
            {
                return new FabricTransportServiceRemotingClientFactory(
                    new FabricTransportRemotingSettings
                    {
                        ExceptionDeserializationTechnique = FabricTransportRemotingSettings.ExceptionDeserialization.Default
                    },
                    callbackClient);
            });

            var serviceUri = new Uri("fabric:/Zadatak/ValidationService");
            _validationService = serviceProxyFactory.CreateServiceProxy<IValidation>(serviceUri);
        }
        public IActionResult Index()
        {
            return View();
        }

        [HttpPost]
        public async Task<IActionResult> AddBook(string title, int quantity)
        {
            //var book = new Book { Title = title, Quantity = quantity, Price = price };
            var isValid = await _validationService.ValidateBookAsync(title, quantity);

            if (isValid)
            {
                TempData["Message"] = "Book added successfully!";
            }
            else
            {
                TempData["Message"] = "Invalid book data.";
            }

            return RedirectToAction("Index", "Home");
        }

        public IActionResult Privacy()
        {
            return View();
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }
    }
}
