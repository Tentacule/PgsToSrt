using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PgsToSrt.Options
{
    internal class TrackOption
    {
        public string Input { get; set; }
        public string Output { get; set; }
        public int? Track { get; set; }
    }
}
