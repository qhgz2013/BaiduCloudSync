using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace BaiduCloudSync.db.mapper
{
    public interface IDatabaseMapper
    {
        void insert(System.Data.IDbConnection connection, object insert_obj);
        object update(System.Data.IDbConnection connection, object update_obj);
    }
}
