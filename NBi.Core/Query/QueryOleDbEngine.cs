﻿using System;
using System.Data;
using System.Data.OleDb;
using System.Diagnostics;

namespace NBi.Core.Query
{
    /// <summary>
    /// Engine wrapping the System.Data.OleDb namespace for execution of NBi tests
    /// <remarks>Instances of this class are built by the means of the <see>QueryEngineFactory</see></remarks>
    /// </summary>
    internal class QueryOleDbEngine: IQueryEnginable, IQueryExecutor, IQueryPerformance, IQueryParser
    {
        protected readonly OleDbCommand command;


        protected internal QueryOleDbEngine(OleDbCommand command)
        {
            this.command = command;
        }

        /// <summary>
        /// Method exposed by the interface IQueryPerformance to execute a test of syntax
        /// </summary>
        /// <remarks>This method makes usage the set statement named SET FMTONLY to not effectively execute the query but check the validity of this query</remarks>
        /// <returns></returns>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Security", "CA2100:Review SQL queries for security vulnerabilities")]
        public virtual ParserResult Parse()
        {
            ParserResult res=null;
            
            using(var conn = new OleDbConnection(command.Connection.ConnectionString))
            {
                var fullSql = string.Format(@"SET FMTONLY ON {0} SET FMTONLY OFF", command.CommandText);
                
                conn.Open();

                using (var cmdIn = new OleDbCommand(fullSql, conn))
                {
                    try 
	                {
                        cmdIn.ExecuteNonQuery();
                        res = ParserResult.NoParsingError();
	                }
	                catch (OleDbException ex)
	                {
                        res = new ParserResult(ex.Message.Split(new string[] { "\r\n" }, System.StringSplitOptions.RemoveEmptyEntries));
	                }
                    
                }

                if (conn.State != System.Data.ConnectionState.Closed)
                conn.Close();
            }

            return res;
        }

        /// <summary>
        /// Method exposed by the interface IQueryPerformance to clean the cache of a SQL Database
        /// <remarks>Makes usage of the command dbcc freeproccache and dbcc dropcleanbuffers</remarks>
        /// </summary>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Security", "CA2100:Review SQL queries for security vulnerabilities")]
        public virtual void CleanCache()
        {
            using (var conn = new OleDbConnection(command.Connection.ConnectionString))
            {
                var clearSql = new string[] { "dbcc freeproccache", "dbcc dropcleanbuffers" };

                conn.Open();

                foreach (var sql in clearSql)
                {
                    using (var cleanCmd = new OleDbCommand(sql, conn))
                    {
                        cleanCmd.ExecuteNonQuery();
                    }
                }
            }
        }

        public PerformanceResult CheckPerformance()
        {
            return CheckPerformance(0);
        }

        public PerformanceResult CheckPerformance(int timeout)
        {
            bool isTimeout = false;
            DateTime tsStart, tsStop = DateTime.Now;

            if (command.Connection.State == ConnectionState.Closed)
                command.Connection.Open();

            tsStart = DateTime.Now;
            try
            {
                command.ExecuteNonQuery();
                tsStop = DateTime.Now;
            }
            catch (OleDbException e)
            {
                if (!e.Message.StartsWith("Timeout expired."))
                    throw;
                isTimeout = true;
            }

            if (command.Connection.State == ConnectionState.Open)
                command.Connection.Close();

            if (isTimeout)
                return PerformanceResult.Timeout(timeout);
            else
                return new PerformanceResult(tsStop.Subtract(tsStart));
        }

        public virtual DataSet Execute()
        {
            float i;
            return Execute(out i);
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Security", "CA2100:Review SQL queries for security vulnerabilities")]
        public virtual DataSet Execute(out float elapsedSec)
        {
            // Open the connection
            using (var connection = new OleDbConnection())
            {
                var connectionString = command.Connection.ConnectionString;
                try
                    { connection.ConnectionString = connectionString; }
                catch (ArgumentException ex)
                { throw new ConnectionException(ex, connectionString); }

                try
                    {connection.Open();}
                catch (OleDbException ex)
                { throw new ConnectionException(ex, connectionString); }

                Trace.WriteLineIf(NBiTraceSwitch.TraceVerbose, command.CommandText);
                // capture time before execution
                DateTime timeBefore = DateTime.Now;
                var adapter = new OleDbDataAdapter(command);
                var ds = new DataSet();

                try
                {
                    adapter.Fill(ds);
                }
                catch (OleDbException ex)
                {
                    if (ex.Message == "Query timeout expired")
                        throw new CommandTimeoutException(ex, adapter.SelectCommand);
                    throw;
                }

                // capture time after execution
                DateTime timeAfter = DateTime.Now;

                // setting query runtime
                elapsedSec = (float)timeAfter.Subtract(timeBefore).TotalSeconds;
                Trace.WriteLineIf(NBiTraceSwitch.TraceInfo, string.Format("Time needed to execute query: {0}", timeAfter.Subtract(timeBefore).ToString(@"d\d\.hh\h\:mm\m\:ss\s\ \+fff\m\s")));

                return ds;
            }
        }
    }
}
