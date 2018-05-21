using System;
using System.Collections.Generic;
using System.Text;

namespace DTO
{
    [Serializable]
    public class Message
    {
        public string UserName { get; set; }
        public string UserMessage { get; set; }

    }
}
