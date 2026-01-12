using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Services.Dtos
{
    public class PagingQuery
    {
        public int Page { get; set; } = 1;
        public int PageSize { get; set; } = 10;

        public int Skip => (Page - 1) * PageSize;
    }
}
