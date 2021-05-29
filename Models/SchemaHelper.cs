using Newtonsoft.Json.Schema;
using Newtonsoft.Json.Schema.Generation;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace QuickSearch.Models
{
    public class SchemaHelper<T>
    {
        private static JSchema schema = null;
        public static JSchema Schema
        {
            get
            {
                if (schema == null)
                {
                    JSchemaGenerator generator = new JSchemaGenerator();
                    schema = generator.Generate(typeof(T));
                }
                return schema;
            }
        }
    }
}
