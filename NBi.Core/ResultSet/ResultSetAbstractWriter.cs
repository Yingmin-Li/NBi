﻿using System;
using System.Data;

namespace NBi.Core.ResultSet
{
    public abstract class ResultSetAbstractWriter: IResultSetWriter
    {
        public string PersistencePath { get; set; }

        public ResultSetAbstractWriter(string persistancePath)
        {
            PersistencePath = persistancePath;
        }

        public void Write(string filename, ResultSet rs)
        {          
            if (rs.Table == null)
                throw new Exception("The underlying Table of the ResultSet cannot be null");

            OnWrite(filename, rs);
        }

        public void Write(string filename, DataSet ds)
        {
            if (ds.Tables.Count == 0)
                throw new Exception("The DataSet contains no table");

            if (ds.Tables.Count > 1)
                throw new Exception("The DataSet has more than one table");

            OnWrite(filename, ds, ds.Tables[0].TableName);
        }

        public void Write(string filename, DataSet ds, string tableName)
        {
            if (!ds.Tables.Contains(tableName))
                throw new Exception(string.Format("The dataset doesn't contain a table named '{0}'", tableName));

            OnWrite(filename, ds, ds.Tables[0].TableName);
        }

        protected abstract void OnWrite(string filename, DataSet ds, string tableName);
        protected abstract void OnWrite(string filename, ResultSet rs);
    }
}
