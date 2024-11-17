using Microsoft.ServiceFabric.Services.Remoting;
using Models;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace Interfaces
{
    public interface IValidation: IService
    {
        Task ValidateBookAsync(string title, int quantity, string client);
    }
}
