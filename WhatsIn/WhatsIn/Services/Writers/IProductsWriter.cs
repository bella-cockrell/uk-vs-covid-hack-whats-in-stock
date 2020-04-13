﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using WhatsIn.Models;

namespace WhatsIn.Services.Writers
{
    public interface IProductsWriter
    {
        Product AddProductToDb(Product productToAdd);

        void UpdateExistingProductInDb(Product product);
    }
}
