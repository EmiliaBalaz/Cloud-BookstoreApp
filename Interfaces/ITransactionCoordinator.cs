﻿using Microsoft.ServiceFabric.Services.Remoting;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace Interfaces
{
    public interface ITransactionCoordinator : IService
    {
        Task StartTransaction(string title, int quantity, string client);
    }
}
