﻿using Microsoft.ServiceFabric.Services.Remoting;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace Interfaces
{

    public interface ITransaction : IService
    {
        Task<bool> Prepare();
        Task Commit();
        Task Rollback();
    }
}
