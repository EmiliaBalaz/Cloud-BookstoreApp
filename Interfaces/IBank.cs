using Microsoft.ServiceFabric.Services.Remoting;
using System;
using Models;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace Interfaces
{
    public interface IBank : IService, ITransaction
    {
        Task<Dictionary<string, Client>> ListClients();
        Task EnlistMoneyTransfer(Guid transactionId, string userID, double amount);
    }
}
