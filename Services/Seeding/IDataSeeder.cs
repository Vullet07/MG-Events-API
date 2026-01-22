using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Services.Seeding
{
    public interface IDataSeeder
    {
        Task SeedAsync();
    }
}
