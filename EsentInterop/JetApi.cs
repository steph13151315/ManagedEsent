﻿//-----------------------------------------------------------------------
// <copyright file="JetApi.cs" company="Microsoft Corporation">
//     Copyright (c) Microsoft Corporation.
// </copyright>
//-----------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Isam.Esent.Interop.Vista;

namespace Microsoft.Isam.Esent.Interop.Implementation
{
    /// <summary>
    /// Calls to the ESENT interop layer. These calls take the managed types (e.g. JET_SESID) and
    /// return errors.
    /// </summary>
    internal class JetApi : IJetApi
    {
        /// <summary>
        /// API call tracing.
        /// </summary>
        private readonly TraceSwitch traceSwitch = new TraceSwitch("ESENT P/Invoke", "P/Invoke calls to ESENT");

        /// <summary>
        /// The version of esent. If this is zero then it is looked up
        /// with <see cref="JetGetVersion"/>.
        /// </summary>
        private readonly uint versionOverride;

        /// <summary>
        /// Initializes a new instance of the JetApi class. This allows the version
        /// to be set.
        /// </summary>
        /// <param name="version">
        /// The version of Esent. This is used to override the results of
        /// <see cref="JetGetVersion"/>.
        /// </param>
        public JetApi(uint version)
        {
            this.versionOverride = version;
            this.DetermineCapabilities();
        }

        /// <summary>
        /// Initializes a new instance of the JetApi class.
        /// </summary>
        public JetApi()
        {
            Debug.Assert(0 == this.versionOverride, "Expected version to be 0");
            this.DetermineCapabilities();
        }

        #region init/term

        public JetCapabilities Capabilities { get; private set; }

        public int JetCreateInstance(out JET_INSTANCE instance, string name)
        {
            this.TraceFunctionCall("JetCreateInstance");
            instance.Value = IntPtr.Zero;
            return this.Err(NativeMethods.JetCreateInstance(out instance.Value, name));
        }

        /// <summary>
        /// Allocate a new instance of the database engine for use in a single
        /// process, with a display name specified.
        /// </summary>
        /// <param name="instance">Returns the newly create instance.</param>
        /// <param name="name">
        /// Specifies a unique string identifier for the instance to be created.
        /// This string must be unique within a given process hosting the
        /// database engine.
        /// </param>
        /// <param name="displayName">
        /// A display name for the instance to be created. This will be used
        /// in eventlog entries.
        /// </param>
        /// <param name="grbit">Creation options.</param>
        /// <returns>An error if the call fails.</returns>
        public int JetCreateInstance2(out JET_INSTANCE instance, string name, string displayName, CreateInstanceGrbit grbit)
        {
            this.TraceFunctionCall("JetCreateInstance2");
            instance.Value = IntPtr.Zero;
            return this.Err(NativeMethods.JetCreateInstance2(out instance.Value, name, displayName, (uint)grbit));
        }

        public int JetInit(ref JET_INSTANCE instance)
        {
            this.TraceFunctionCall("JetInit");
            return this.Err(NativeMethods.JetInit(ref instance.Value));
        }

        /// <summary>
        /// Initialize the ESENT database engine.
        /// </summary>
        /// <param name="instance">
        /// The instance to initialize. If an instance hasn't been
        /// allocated then a new one is created and the engine
        /// will operate in single-instance mode.
        /// </param>
        /// <param name="grbit">
        /// Initialization options.
        /// </param>
        /// <returns>An error or a warning.</returns>
        public int JetInit2(ref JET_INSTANCE instance, InitGrbit grbit)
        {
            this.TraceFunctionCall("JetInit2");
            return this.Err(NativeMethods.JetInit2(ref instance.Value, (uint) grbit));
        }

        public int JetTerm(JET_INSTANCE instance)
        {
            this.TraceFunctionCall("JetTerm");
            if (JET_INSTANCE.Nil != instance)
            {
                return this.Err(NativeMethods.JetTerm(instance.Value));
            }

            return (int)JET_err.Success;
        }

        public int JetTerm2(JET_INSTANCE instance, TermGrbit grbit)
        {
            this.TraceFunctionCall("JetTerm2");
            if (JET_INSTANCE.Nil != instance)
            {
                return this.Err(NativeMethods.JetTerm2(instance.Value, (uint) grbit));
            }

            return (int)JET_err.Success;
        }

        public int JetSetSystemParameter(JET_INSTANCE instance, JET_SESID sesid, JET_param paramid, int paramValue, string paramString)
        {
            this.TraceFunctionCall("JetSetSystemParameter");
            unsafe
            {
                IntPtr* pinstance = (IntPtr.Zero == instance.Value) ? null : &instance.Value;
                if (this.Capabilities.SupportsUnicodePaths)
                {
                    return this.Err(NativeMethods.JetSetSystemParameterW(pinstance, sesid.Value, (uint)paramid, (IntPtr)paramValue, paramString));
                }

                return this.Err(NativeMethods.JetSetSystemParameter(pinstance, sesid.Value, (uint)paramid, (IntPtr)paramValue, paramString));                
            }
        }

        public int JetGetSystemParameter(JET_INSTANCE instance, JET_SESID sesid, JET_param paramid, ref int paramValue, out string paramString, int maxParam)
        {
            this.TraceFunctionCall("JetGetSystemParameter");

            var intValue = (IntPtr)paramValue;
            var sb = new StringBuilder(maxParam);
            int err;
            if (this.Capabilities.SupportsUnicodePaths)
            {
                err = this.Err(NativeMethods.JetGetSystemParameterW(instance.Value, sesid.Value, (uint)paramid, ref intValue, sb, (uint)maxParam * sizeof(char)));
            }
            else
            {
                err = this.Err(NativeMethods.JetGetSystemParameter(instance.Value, sesid.Value, (uint)paramid, ref intValue, sb, (uint)maxParam));                
            }

            paramString = sb.ToString();
            paramValue = intValue.ToInt32();
            return err;
        }

        /// <summary>
        /// Retrieves the version of the database engine.
        /// </summary>
        /// <param name="sesid">The session to use.</param>
        /// <param name="version">Returns the version number of the database engine.</param>
        /// <returns>An error code if the call fails.</returns>
        public int JetGetVersion(JET_SESID sesid, out uint version)
        {
            this.TraceFunctionCall("JetGetVersion");
            uint nativeVersion;
            int err;

            if (0 != this.versionOverride)
            {
                // We have an explicitly set version
                Trace.WriteLineIf(
                    this.traceSwitch.TraceVerbose, String.Format("JetGetVersion overridden with 0x{0:X}", this.versionOverride));
                nativeVersion = this.versionOverride;
                err = 0;
            }
            else
            {
                // Get the version from Esent
                err = this.Err(NativeMethods.JetGetVersion(sesid.Value, out nativeVersion));                
            }

            version = nativeVersion;
            return err;
        }

        #endregion

        #region Databases

        public int JetCreateDatabase(JET_SESID sesid, string database, string connect, out JET_DBID dbid, CreateDatabaseGrbit grbit)
        {
            this.TraceFunctionCall("JetCreateDatabase");
            this.CheckNotNull(database, "database");

            dbid = JET_DBID.Nil;
            if (this.Capabilities.SupportsUnicodePaths)
            {
                return this.Err(NativeMethods.JetCreateDatabaseW(sesid.Value, database, connect, out dbid.Value, (uint)grbit));
            }

            return this.Err(NativeMethods.JetCreateDatabase(sesid.Value, database, connect, out dbid.Value, (uint)grbit));
        }

        public int JetAttachDatabase(JET_SESID sesid, string database, AttachDatabaseGrbit grbit)
        {
            this.TraceFunctionCall("JetAttachDatabase");
            this.CheckNotNull(database, "database");

            if (this.Capabilities.SupportsUnicodePaths)
            {
                return this.Err(NativeMethods.JetAttachDatabaseW(sesid.Value, database, (uint)grbit));                
            }

            return this.Err(NativeMethods.JetAttachDatabase(sesid.Value, database, (uint)grbit));
        }

        public int JetOpenDatabase(JET_SESID sesid, string database, string connect, out JET_DBID dbid, OpenDatabaseGrbit grbit)
        {
            this.TraceFunctionCall("JetOpenDatabase");
            dbid = JET_DBID.Nil;
            this.CheckNotNull(database, "database");

            if (this.Capabilities.SupportsUnicodePaths)
            {
                return this.Err(NativeMethods.JetOpenDatabaseW(sesid.Value, database, connect, out dbid.Value, (uint)grbit));                
            }

            return this.Err(NativeMethods.JetOpenDatabase(sesid.Value, database, connect, out dbid.Value, (uint)grbit));
        }

        public int JetCloseDatabase(JET_SESID sesid, JET_DBID dbid, CloseDatabaseGrbit grbit)
        {
            this.TraceFunctionCall("JetCloseDatabase");
            return this.Err(NativeMethods.JetCloseDatabase(sesid.Value, dbid.Value, (uint)grbit));
        }

        public int JetDetachDatabase(JET_SESID sesid, string database)
        {
            this.TraceFunctionCall("JetDetachDatabase");
            this.CheckNotNull(database, "database");

            if (this.Capabilities.SupportsUnicodePaths)
            {
                return this.Err(NativeMethods.JetDetachDatabaseW(sesid.Value, database));                
            }

            return this.Err(NativeMethods.JetDetachDatabase(sesid.Value, database));
        }

        #endregion

        #region sessions

        public int JetBeginSession(JET_INSTANCE instance, out JET_SESID sesid, string username, string password)
        {
            this.TraceFunctionCall("JetBeginSession");
            sesid = JET_SESID.Nil;
            return this.Err(NativeMethods.JetBeginSession(instance.Value, out sesid.Value, null, null));
        }

        /// <summary>
        /// Associates a session with the current thread using the given context
        /// handle. This association overrides the default engine requirement
        /// that a transaction for a given session must occur entirely on the
        /// same thread. 
        /// </summary>
        /// <param name="sesid">The session to set the context on.</param>
        /// <param name="context">The context to set.</param>
        /// <returns>An error if the call fails.</returns>
        public int JetSetSessionContext(JET_SESID sesid, IntPtr context)
        {
            this.TraceFunctionCall("JetSetSessionContext");
            return this.Err(NativeMethods.JetSetSessionContext(sesid.Value, context));
        }

        /// <summary>
        /// Disassociates a session from the current thread. This should be
        /// used in conjunction with JetSetSessionContext.
        /// </summary>
        /// <param name="sesid">The session to use.</param>
        /// <returns>An error if the call fails.</returns>
        public int JetResetSessionContext(JET_SESID sesid)
        {
            this.TraceFunctionCall("JetResetSessionContext");
            return this.Err(NativeMethods.JetResetSessionContext(sesid.Value));
        }

        public int JetEndSession(JET_SESID sesid, EndSessionGrbit grbit)
        {
            this.TraceFunctionCall("JetEndSession");
            return this.Err(NativeMethods.JetEndSession(sesid.Value, (uint)grbit));
        }

        public int JetDupSession(JET_SESID sesid, out JET_SESID newSesid)
        {
            this.TraceFunctionCall("JetDupSession");
            newSesid = JET_SESID.Nil;
            return this.Err(NativeMethods.JetDupSession(sesid.Value, out newSesid.Value));
        }

        #endregion

        #region tables

        public int JetOpenTable(JET_SESID sesid, JET_DBID dbid, string tablename, byte[] parameters, int parametersLength, OpenTableGrbit grbit, out JET_TABLEID tableid)
        {
            this.TraceFunctionCall("JetOpenTable");
            tableid = JET_TABLEID.Nil;
            this.CheckNotNull(tablename, "tablename");

            return this.Err(NativeMethods.JetOpenTable(sesid.Value, dbid.Value, tablename, IntPtr.Zero, 0, (uint)grbit, out tableid.Value));
        }

        public int JetCloseTable(JET_SESID sesid, JET_TABLEID tableid)
        {
            this.TraceFunctionCall("JetCloseTable");
            return this.Err(NativeMethods.JetCloseTable(sesid.Value, tableid.Value));
        }

        public int JetDupCursor(JET_SESID sesid, JET_TABLEID tableid, out JET_TABLEID newTableid, DupCursorGrbit grbit)
        {
            this.TraceFunctionCall("JetDupCursor");
            newTableid = JET_TABLEID.Nil;
            return this.Err(NativeMethods.JetDupCursor(sesid.Value, tableid.Value, out newTableid.Value, (uint)grbit));
        }

        #endregion

        #region transactions

        public int JetBeginTransaction(JET_SESID sesid)
        {
            this.TraceFunctionCall("JetBeginTransaction");
            return this.Err(NativeMethods.JetBeginTransaction(sesid.Value));
        }

        public int JetCommitTransaction(JET_SESID sesid, CommitTransactionGrbit grbit)
        {
            this.TraceFunctionCall("JetCommitTransaction");
            return this.Err(NativeMethods.JetCommitTransaction(sesid.Value, (uint)grbit));
        }

        public int JetRollback(JET_SESID sesid, RollbackTransactionGrbit grbit)
        {
            this.TraceFunctionCall("JetRollback");
            return this.Err(NativeMethods.JetRollback(sesid.Value, (uint)grbit));
        }

        #endregion

        #region DDL

        public int JetCreateTable(JET_SESID sesid, JET_DBID dbid, string table, int pages, int density, out JET_TABLEID tableid)
        {
            this.TraceFunctionCall("JetCreateTable");
            tableid = JET_TABLEID.Nil;
            this.CheckNotNull(table, "table");

            return this.Err(NativeMethods.JetCreateTable(sesid.Value, dbid.Value, table, pages, density, out tableid.Value));
        }

        public int JetAddColumn(JET_SESID sesid, JET_TABLEID tableid, string column, JET_COLUMNDEF columndef, byte[] defaultValue, int defaultValueSize, out JET_COLUMNID columnid)
        {
            this.TraceFunctionCall("JetAddColumn");
            columnid = JET_COLUMNID.Nil;
            this.CheckNotNull(column, "column");
            this.CheckNotNull(columndef, "columndef");
            this.CheckDataSize(defaultValue, defaultValueSize, "defaultValueSize");

            NATIVE_COLUMNDEF nativeColumndef = columndef.GetNativeColumndef();
            int err = this.Err(NativeMethods.JetAddColumn(
                                   sesid.Value, 
                                   tableid.Value, 
                                   column, 
                                   ref nativeColumndef,
                                   defaultValue, 
                                   (uint) defaultValueSize,
                                   out columnid.Value));

            // esent doesn't actually set the columnid member of the passed in JET_COLUMNDEF, but we will do that here for
            // completeness.
            columndef.columnid = new JET_COLUMNID { Value = columnid.Value };
            return err;
        }

        public int JetDeleteColumn(JET_SESID sesid, JET_TABLEID tableid, string column)
        {
            this.TraceFunctionCall("JetDeleteColumn");
            this.CheckNotNull(column, "column");

            return this.Err(NativeMethods.JetDeleteColumn(sesid.Value, tableid.Value, column));
        }

        public int JetDeleteIndex(JET_SESID sesid, JET_TABLEID tableid, string index)
        {
            this.TraceFunctionCall("JetDeleteIndex");
            this.CheckNotNull(index, "index");

            return this.Err(NativeMethods.JetDeleteIndex(sesid.Value, tableid.Value, index));
        }

        public int JetDeleteTable(JET_SESID sesid, JET_DBID dbid, string table)
        {
            this.TraceFunctionCall("JetDeleteTable");
            this.CheckNotNull(table, "table");

            return this.Err(NativeMethods.JetDeleteTable(sesid.Value, dbid.Value, table));
        }

        public int JetCreateIndex(
            JET_SESID sesid,
            JET_TABLEID tableid,
            string indexName,
            CreateIndexGrbit grbit, 
            string keyDescription,
            int keyDescriptionLength,
            int density)
        {
            this.TraceFunctionCall("JetCreateIndex");
            this.CheckNotNull(indexName, "indexName");
            this.CheckNotNegative(keyDescriptionLength, "keyDescriptionLength");
            this.CheckNotNegative(density, "density");
            if (keyDescriptionLength > keyDescription.Length + 1)
            {
                throw new ArgumentOutOfRangeException(
                    "keyDescriptionLength", keyDescriptionLength, "cannot be greater than keyDescription.Length");
            }

            return this.Err(NativeMethods.JetCreateIndex(
                sesid.Value,
                tableid.Value,
                indexName,
                (uint)grbit,
                keyDescription,
                (uint)keyDescriptionLength,
                (uint)density));
        }

        /// <summary>
        /// Creates indexes over data in an ESE database
        /// </summary>
        /// <param name="sesid">The session to use.</param>
        /// <param name="tableid">The table to create the index on.</param>
        /// <param name="indexcreates">Array of objects describing the indexes to be created.</param>
        /// <param name="numIndexCreates">Number of index description objects.</param>
        /// <returns>An error code.</returns>
        public int JetCreateIndex2(
            JET_SESID sesid,
            JET_TABLEID tableid,
            JET_INDEXCREATE[] indexcreates,
            int numIndexCreates)
        {
            this.TraceFunctionCall("JetCreateIndex2");
            this.CheckNotNull(indexcreates, "indexcreates");
            this.CheckNotNegative(numIndexCreates, "numIndexCreates");
            if (numIndexCreates > indexcreates.Length)
            {
                throw new ArgumentOutOfRangeException(
                    "numIndexCreates", numIndexCreates, "numIndexCreates is larger than the number of indexes passed in");
            }

            if (this.Capabilities.SupportsVistaFeatures)
            {
                return this.CreateIndexes2(sesid, tableid, indexcreates, numIndexCreates);                
            }

            return this.CreateIndexes(sesid, tableid, indexcreates, numIndexCreates);
        }

        /// <summary>
        /// Creates a temporary table with a single index. A temporary table
        /// stores and retrieves records just like an ordinary table created
        /// using JetCreateTableColumnIndex. However, temporary tables are
        /// much faster than ordinary tables due to their volatile nature.
        /// They can also be used to very quickly sort and perform duplicate
        /// removal on record sets when accessed in a purely sequential manner.
        /// </summary>
        /// <param name="sesid">The session to use.</param>
        /// <param name="columns">
        /// Column definitions for the columns created in the temporary table.
        /// </param>
        /// <param name="numColumns">Number of column definitions.</param>
        /// <param name="grbit">Table creation options.</param>
        /// <param name="tableid">
        /// Returns the tableid of the temporary table. Closing this tableid
        /// frees the resources associated with the temporary table.
        /// </param>
        /// <param name="columnids">
        /// The output buffer that receives the array of column IDs generated
        /// during the creation of the temporary table. The column IDs in this
        /// array will exactly correspond to the input array of column definitions.
        /// As a result, the size of this buffer must correspond to the size of the input array.
        /// </param>
        /// <returns>An error code.</returns>
        public int JetOpenTempTable(
            JET_SESID sesid,
            JET_COLUMNDEF[] columns,
            int numColumns,
            TempTableGrbit grbit,
            out JET_TABLEID tableid,
            JET_COLUMNID[] columnids)
        {
            this.TraceFunctionCall("JetOpenTempTable");
            this.CheckNotNull(columns, "columnns");
            this.CheckDataSize(columns, numColumns, "numColumns");
            this.CheckNotNull(columnids, "columnids");
            this.CheckDataSize(columnids, numColumns, "numColumns");

            tableid = JET_TABLEID.Nil;

            NATIVE_COLUMNDEF[] nativecolumndefs = GetNativecolumndefs(columns, numColumns);
            var nativecolumnids = new uint[numColumns];

            int err = NativeMethods.JetOpenTempTable(
                sesid.Value, nativecolumndefs, (uint) numColumns, (uint) grbit, out tableid.Value, nativecolumnids);

            SetColumnids(columnids, nativecolumnids, numColumns);
            
            return err;
        }

        /// <summary>
        /// Creates a temporary table with a single index. A temporary table
        /// stores and retrieves records just like an ordinary table created
        /// using JetCreateTableColumnIndex. However, temporary tables are
        /// much faster than ordinary tables due to their volatile nature.
        /// They can also be used to very quickly sort and perform duplicate
        /// removal on record sets when accessed in a purely sequential manner.
        /// </summary>
        /// <param name="sesid">The session to use.</param>
        /// <param name="columns">
        /// Column definitions for the columns created in the temporary table.
        /// </param>
        /// <param name="numColumns">Number of column definitions.</param>
        /// <param name="lcid">
        /// The locale ID to use to compare any Unicode key column data in the temporary table.
        /// Any locale may be used as long as the appropriate language pack has been installed
        /// on the machine. 
        /// </param>
        /// <param name="grbit">Table creation options.</param>
        /// <param name="tableid">
        /// Returns the tableid of the temporary table. Closing this tableid
        /// frees the resources associated with the temporary table.
        /// </param>
        /// <param name="columnids">
        /// The output buffer that receives the array of column IDs generated
        /// during the creation of the temporary table. The column IDs in this
        /// array will exactly correspond to the input array of column definitions.
        /// As a result, the size of this buffer must correspond to the size of the input array.
        /// </param>
        /// <returns>An error code.</returns>
        public int JetOpenTempTable2(
            JET_SESID sesid,
            JET_COLUMNDEF[] columns,
            int numColumns,
            int lcid,
            TempTableGrbit grbit,
            out JET_TABLEID tableid,
            JET_COLUMNID[] columnids)
        {
            this.TraceFunctionCall("JetOpenTempTable2");
            this.CheckNotNull(columns, "columnns");
            this.CheckDataSize(columns, numColumns, "numColumns");
            this.CheckNotNull(columnids, "columnids");
            this.CheckDataSize(columnids, numColumns, "numColumns");

            tableid = JET_TABLEID.Nil;

            NATIVE_COLUMNDEF[] nativecolumndefs = GetNativecolumndefs(columns, numColumns);
            var nativecolumnids = new uint[numColumns];

            int err = NativeMethods.JetOpenTempTable2(
                sesid.Value, nativecolumndefs, (uint)numColumns, (uint) lcid, (uint)grbit, out tableid.Value, nativecolumnids);

            SetColumnids(columnids, nativecolumnids, numColumns);

            return err;            
        }

        /// <summary>
        /// Creates a temporary table with a single index. A temporary table
        /// stores and retrieves records just like an ordinary table created
        /// using JetCreateTableColumnIndex. However, temporary tables are
        /// much faster than ordinary tables due to their volatile nature.
        /// They can also be used to very quickly sort and perform duplicate
        /// removal on record sets when accessed in a purely sequential manner.
        /// </summary>
        /// <param name="sesid">The session to use.</param>
        /// <param name="columns">
        /// Column definitions for the columns created in the temporary table.
        /// </param>
        /// <param name="numColumns">Number of column definitions.</param>
        /// <param name="unicodeindex">
        /// The Locale ID and normalization flags that will be used to compare
        /// any Unicode key column data in the temporary table. When this 
        /// is not present then the default options are used. 
        /// </param>
        /// <param name="grbit">Table creation options.</param>
        /// <param name="tableid">
        /// Returns the tableid of the temporary table. Closing this tableid
        /// frees the resources associated with the temporary table.
        /// </param>
        /// <param name="columnids">
        /// The output buffer that receives the array of column IDs generated
        /// during the creation of the temporary table. The column IDs in this
        /// array will exactly correspond to the input array of column definitions.
        /// As a result, the size of this buffer must correspond to the size of the input array.
        /// </param>
        /// <returns>An error code.</returns>
        public int JetOpenTempTable3(
            JET_SESID sesid,
            JET_COLUMNDEF[] columns,
            int numColumns,
            JET_UNICODEINDEX unicodeindex,
            TempTableGrbit grbit,
            out JET_TABLEID tableid,
            JET_COLUMNID[] columnids)
        {
            this.TraceFunctionCall("JetOpenTempTable3");
            this.CheckNotNull(columns, "columnns");
            this.CheckDataSize(columns, numColumns, "numColumns");
            this.CheckNotNull(columnids, "columnids");
            this.CheckDataSize(columnids, numColumns, "numColumns");

            tableid = JET_TABLEID.Nil;

            NATIVE_COLUMNDEF[] nativecolumndefs = GetNativecolumndefs(columns, numColumns);
            var nativecolumnids = new uint[numColumns];

            int err;
            if (null != unicodeindex)
            {
                NATIVE_UNICODEINDEX nativeunicodeindex = unicodeindex.GetNativeUnicodeIndex();
                err = NativeMethods.JetOpenTempTable3(
                    sesid.Value, nativecolumndefs, (uint)numColumns, ref nativeunicodeindex, (uint)grbit, out tableid.Value, nativecolumnids);
            }
            else
            {
                err = NativeMethods.JetOpenTempTable3(
                    sesid.Value, nativecolumndefs, (uint)numColumns, IntPtr.Zero, (uint)grbit, out tableid.Value, nativecolumnids);                
            }

            SetColumnids(columnids, nativecolumnids, numColumns);

            return err;            
        }

        public int JetGetTableColumnInfo(
                JET_SESID sesid,
                JET_TABLEID tableid,
                string columnName,
                out JET_COLUMNDEF columndef)
        {
            this.TraceFunctionCall("JetGetTableColumnInfo");
            columndef = new JET_COLUMNDEF();
            this.CheckNotNull(columnName, "columnName");

            var nativeColumndef = new NATIVE_COLUMNDEF();
            nativeColumndef.cbStruct = (uint)Marshal.SizeOf(nativeColumndef);
            int err = this.Err(NativeMethods.JetGetTableColumnInfo(
                sesid.Value,
                tableid.Value,
                columnName,
                ref nativeColumndef,
                nativeColumndef.cbStruct,
                (uint)JET_ColInfo.Default));
            columndef.SetFromNativeColumndef(nativeColumndef);

            return err;
        }

        public int JetGetTableColumnInfo(
                JET_SESID sesid,
                JET_TABLEID tableid,
                JET_COLUMNID columnid,
                out JET_COLUMNDEF columndef)
        {
            this.TraceFunctionCall("JetGetTableColumnInfo");
            columndef = new JET_COLUMNDEF();

            var nativeColumndef = new NATIVE_COLUMNDEF();
            nativeColumndef.cbStruct = (uint)Marshal.SizeOf(nativeColumndef);
            int err = this.Err(NativeMethods.JetGetTableColumnInfo(
                sesid.Value,
                tableid.Value,
                ref columnid.Value,
                ref nativeColumndef,
                nativeColumndef.cbStruct,
                (uint)JET_ColInfo.ByColid));
            columndef.SetFromNativeColumndef(nativeColumndef);

            return err;
        }

        public int JetGetTableColumnInfo(
                JET_SESID sesid,
                JET_TABLEID tableid,
                string ignored,
                out JET_COLUMNLIST columnlist)
        {
            this.TraceFunctionCall("JetGetTableColumnInfo");
            columnlist = new JET_COLUMNLIST();

            var nativeColumnlist = new NATIVE_COLUMNLIST();
            nativeColumnlist.cbStruct = (uint)Marshal.SizeOf(nativeColumnlist);
            int err = this.Err(NativeMethods.JetGetTableColumnInfo(
                sesid.Value,
                tableid.Value,
                ignored,
                ref nativeColumnlist,
                nativeColumnlist.cbStruct,
                (uint)JET_ColInfo.List));
            columnlist.SetFromNativeColumnlist(nativeColumnlist);

            return err;
        }

        public int JetGetColumnInfo(
                JET_SESID sesid,
                JET_DBID dbid,
                string tablename,
                string columnName,
                out JET_COLUMNDEF columndef)
        {
            this.TraceFunctionCall("JetGetColumnInfo");
            columndef = new JET_COLUMNDEF();
            this.CheckNotNull(tablename, "tablename");
            this.CheckNotNull(columnName, "columnName");

            var nativeColumndef = new NATIVE_COLUMNDEF();
            nativeColumndef.cbStruct = (uint)Marshal.SizeOf(nativeColumndef);
            int err = this.Err(NativeMethods.JetGetColumnInfo(
               sesid.Value,
               dbid.Value,
               tablename,
               columnName,
               ref nativeColumndef,
               nativeColumndef.cbStruct,
               (uint)JET_ColInfo.Default));
            columndef.SetFromNativeColumndef(nativeColumndef);

            return err;
        }

        public int JetGetColumnInfo(
                JET_SESID sesid,
                JET_DBID dbid,
                string tablename,
                string ignored,
                out JET_COLUMNLIST columnlist)
        {
            this.TraceFunctionCall("JetGetColumnInfo");      
            columnlist = new JET_COLUMNLIST();
            this.CheckNotNull(tablename, "tablename");

            var nativeColumnlist = new NATIVE_COLUMNLIST();
            nativeColumnlist.cbStruct = (uint)Marshal.SizeOf(nativeColumnlist);
            int err = this.Err(NativeMethods.JetGetColumnInfo(
                sesid.Value,
                dbid.Value,
                tablename,
                ignored,
                ref nativeColumnlist,
                nativeColumnlist.cbStruct,
                (uint)JET_ColInfo.List));
            columnlist.SetFromNativeColumnlist(nativeColumnlist);

            return err;
        }

        public int JetGetObjectInfo(JET_SESID sesid, JET_DBID dbid, out JET_OBJECTLIST objectlist)
        {
            this.TraceFunctionCall("JetGetObjectInfo");
            objectlist = new JET_OBJECTLIST();

            var nativeObjectlist = new NATIVE_OBJECTLIST();
            nativeObjectlist.cbStruct = (uint)Marshal.SizeOf(nativeObjectlist);
            int err = this.Err(NativeMethods.JetGetObjectInfo(
                sesid.Value,
                dbid.Value,
                (uint)JET_objtyp.Table,
                null,
                null,
                ref nativeObjectlist,
                nativeObjectlist.cbStruct,
                (uint)JET_ObjInfo.ListNoStats));
            objectlist.SetFromNativeObjectlist(nativeObjectlist);

            return err;
        }

        /// <summary>
        /// JetGetCurrentIndex function determines the name of the current
        /// index of a given cursor. This name is also used to later re-select
        /// that index as the current index using JetSetCurrentIndex. It can
        /// also be used to discover the properties of that index using
        /// JetGetTableIndexInfo.
        /// </summary>
        /// <param name="sesid">The session to use.</param>
        /// <param name="tableid">The cursor to get the index name for.</param>
        /// <param name="indexName">Returns the name of the index.</param>
        /// <param name="maxNameLength">
        /// The maximum length of the index name. Index names are no more than 
        /// Api.MaxNameLength characters.
        /// </param>
        /// <returns>An error if the call fails.</returns>
        public int JetGetCurrentIndex(JET_SESID sesid, JET_TABLEID tableid, out string indexName, int maxNameLength)
        {
            this.TraceFunctionCall("JetGetCurrentIndex");
            this.CheckNotNegative(maxNameLength, "maxNameLength");

            var name = new StringBuilder(maxNameLength);
            int err = this.Err(NativeMethods.JetGetCurrentIndex(sesid.Value, tableid.Value, name, (uint)maxNameLength));
            indexName = name.ToString();
            return err;
        }

        public int JetGetIndexInfo(
                JET_SESID sesid,
                JET_DBID dbid,
                string tablename,
                string ignored,
                out JET_INDEXLIST indexlist)
        {
            this.TraceFunctionCall("JetGetIndexInfo");
            indexlist = new JET_INDEXLIST();
            this.CheckNotNull(tablename, "tablename");

            var nativeIndexlist = new NATIVE_INDEXLIST();
            nativeIndexlist.cbStruct = (uint)Marshal.SizeOf(nativeIndexlist);
            int err = this.Err(NativeMethods.JetGetIndexInfo(
                sesid.Value,
                dbid.Value,
                tablename,
                ignored,
                ref nativeIndexlist,
                nativeIndexlist.cbStruct,
                (uint)JET_IdxInfo.InfoList));
            indexlist.SetFromNativeIndexlist(nativeIndexlist);

            return err;
        }

        public int JetGetTableIndexInfo(
                JET_SESID sesid,
                JET_TABLEID tableid,
                string ignored,
                out JET_INDEXLIST indexlist)
        {
            this.TraceFunctionCall("JetGetTableIndexInfo");
            indexlist = new JET_INDEXLIST();

            var nativeIndexlist = new NATIVE_INDEXLIST();
            nativeIndexlist.cbStruct = (uint)Marshal.SizeOf(nativeIndexlist);
            int err = this.Err(NativeMethods.JetGetTableIndexInfo(
                sesid.Value,
                tableid.Value,
                ignored,
                ref nativeIndexlist,
                nativeIndexlist.cbStruct,
                (uint)JET_IdxInfo.InfoList));
            indexlist.SetFromNativeIndexlist(nativeIndexlist);

            return err;
        }

        #endregion

        #region Navigation

        public int JetGetBookmark(JET_SESID sesid, JET_TABLEID tableid, byte[] bookmark, int bookmarkSize, out int actualBookmarkSize)
        {
            this.TraceFunctionCall("JetGetBookmark");
            this.CheckDataSize(bookmark, bookmarkSize, "bookmarkSize");

            uint cbActual;
            int err = this.Err(
                NativeMethods.JetGetBookmark(
                    sesid.Value,
                    tableid.Value,
                    bookmark, 
                    (uint) bookmarkSize,
                    out cbActual));

            actualBookmarkSize = (int)cbActual;
            return err;
        }

        public int JetGotoBookmark(JET_SESID sesid, JET_TABLEID tableid, byte[] bookmark, int bookmarkSize)
        {
            this.TraceFunctionCall("JetGotoBookmark");
            this.CheckNotNull(bookmark, "bookmark");
            this.CheckDataSize(bookmark, bookmarkSize, "bookmarkSize");

            return
                this.Err(
                    NativeMethods.JetGotoBookmark(
                        sesid.Value, tableid.Value, bookmark, (uint) bookmarkSize));
        }

        public int JetRetrieveColumn(JET_SESID sesid, JET_TABLEID tableid, JET_COLUMNID columnid, IntPtr data, int dataSize, out int actualDataSize, RetrieveColumnGrbit grbit, JET_RETINFO retinfo)
        {
            this.TraceFunctionCall("JetRetrieveColumn");
            this.CheckNotNegative(dataSize, "dataSize");

            int err;
            uint cbActual;
            if (null != retinfo)
            {
                NATIVE_RETINFO nativeRetinfo = retinfo.GetNativeRetinfo();
                err = this.Err(NativeMethods.JetRetrieveColumn(
                        sesid.Value,
                        tableid.Value,
                        columnid.Value,
                        data,
                        (uint)dataSize,
                        out cbActual,
                        (uint)grbit,
                        ref nativeRetinfo));
                retinfo.SetFromNativeRetinfo(nativeRetinfo);
            }
            else
            {
                err = this.Err(NativeMethods.JetRetrieveColumn(
                        sesid.Value,
                        tableid.Value,
                        columnid.Value,
                        data,
                        (uint)dataSize,
                        out cbActual,
                        (uint)grbit,
                        IntPtr.Zero));
            }

            actualDataSize = (int)cbActual;
            return err;
        }

        public int JetMakeKey(JET_SESID sesid, JET_TABLEID tableid, IntPtr data, int dataSize, MakeKeyGrbit grbit)
        {
            this.TraceFunctionCall("JetMakeKey");
            this.CheckNotNegative(dataSize, "dataSize");
            return this.Err(NativeMethods.JetMakeKey(sesid.Value, tableid.Value, data, (uint)dataSize, (uint)grbit));
        }

        public int JetRetrieveKey(JET_SESID sesid, JET_TABLEID tableid, byte[] data, int dataSize, out int actualDataSize, RetrieveKeyGrbit grbit)
        {
            this.TraceFunctionCall("JetRetrieveKey");
            this.CheckDataSize(data, dataSize, "dataSize");

            uint cbActual;
            int err = this.Err(NativeMethods.JetRetrieveKey(sesid.Value, tableid.Value, data, (uint)dataSize, out cbActual, (uint)grbit));

            actualDataSize = (int)cbActual;
            return err;
        }

        public int JetSeek(JET_SESID sesid, JET_TABLEID tableid, SeekGrbit grbit)
        {
            this.TraceFunctionCall("JetSeek");
            return this.Err(NativeMethods.JetSeek(sesid.Value, tableid.Value, (uint)grbit));
        }

        public int JetMove(JET_SESID sesid, JET_TABLEID tableid, int numRows, MoveGrbit grbit)
        {
            this.TraceFunctionCall("JetMove");
            return this.Err(NativeMethods.JetMove(sesid.Value, tableid.Value, numRows, (uint)grbit));
        }

        public int JetSetIndexRange(JET_SESID sesid, JET_TABLEID tableid, SetIndexRangeGrbit grbit)
        {
            this.TraceFunctionCall("JetSetIndexRange");
            return this.Err(NativeMethods.JetSetIndexRange(sesid.Value, tableid.Value, (uint)grbit));
        }

        /// <summary>
        /// Computes the intersection between multiple sets of index entries from different secondary
        /// indices over the same table. This operation is useful for finding the set of records in a
        /// table that match two or more criteria that can be expressed using index ranges. 
        /// </summary>
        /// <param name="sesid">The session to use.</param>
        /// <param name="ranges">
        /// An the index ranges to intersect. The tableids in the ranges
        ///  must have index ranges set on them.
        /// </param>
        /// <param name="numRanges">
        /// The number of index ranges.
        /// </param>
        /// <param name="recordlist">
        /// Returns information about the temporary table containing the intersection results.
        /// </param>
        /// <param name="grbit">Intersection options.</param>
        /// <returns>An error if the call fails.</returns>
        public int JetIntersectIndexes(
            JET_SESID sesid,
            JET_INDEXRANGE[] ranges,
            int numRanges,
            out JET_RECORDLIST recordlist,
            IntersectIndexesGrbit grbit)
        {
            this.TraceFunctionCall("JetIntersectIndexes");
            this.CheckNotNull(ranges, "ranges");
            this.CheckDataSize(ranges, numRanges, "numRanges");
            if (numRanges < 2)
            {
                throw new ArgumentOutOfRangeException(
                    "numRanges", numRanges, "JetIntersectIndexes requires at least two index ranges.");
            }

            var indexRanges = new NATIVE_INDEXRANGE[numRanges];
            for (int i = 0; i < numRanges; ++i)
            {
                indexRanges[i] = ranges[i].GetNativeIndexRange();
            }

            var nativeRecordlist = new NATIVE_RECORDLIST();
            nativeRecordlist.cbStruct = (uint)Marshal.SizeOf(nativeRecordlist);

            int err = this.Err(
                        NativeMethods.JetIntersectIndexes(
                            sesid.Value,
                            indexRanges,
                            (uint) indexRanges.Length,
                            ref nativeRecordlist,
                            (uint) grbit));
            recordlist = new JET_RECORDLIST();
            recordlist.SetFromNativeRecordlist(nativeRecordlist);
            return err;
        }

        public int JetSetCurrentIndex(JET_SESID sesid, JET_TABLEID tableid, string index)
        {
            this.TraceFunctionCall("JetSetCurrentIndex");

            // A null index name is valid here -- it will set the table to the primary index
            return this.Err(NativeMethods.JetSetCurrentIndex(sesid.Value, tableid.Value, index));
        }

        public int JetIndexRecordCount(JET_SESID sesid, JET_TABLEID tableid, out int numRecords, int maxRecordsToCount)
        {
            this.TraceFunctionCall("JetIndexRecordCount");
            this.CheckNotNegative(maxRecordsToCount, "maxRecordsToCount");
            uint crec;
            int err = this.Err(NativeMethods.JetIndexRecordCount(sesid.Value, tableid.Value, out crec, (uint)maxRecordsToCount));
            numRecords = (int)crec;
            return err;
        }

        public int JetSetTableSequential(JET_SESID sesid, JET_TABLEID tableid, SetTableSequentialGrbit grbit)
        {
            this.TraceFunctionCall("JetSetTableSequential");
            return this.Err(NativeMethods.JetSetTableSequential(sesid.Value, tableid.Value, (uint)grbit));
        }

        public int JetResetTableSequential(JET_SESID sesid, JET_TABLEID tableid, ResetTableSequentialGrbit grbit)
        {
            this.TraceFunctionCall("JetResetTableSequential");
            return this.Err(NativeMethods.JetResetTableSequential(sesid.Value, tableid.Value, (uint)grbit));
        }

        public int JetGetRecordPosition(JET_SESID sesid, JET_TABLEID tableid, out JET_RECPOS recpos)
        {
            this.TraceFunctionCall("JetGetRecordPosition");
            recpos = new JET_RECPOS();
            NATIVE_RECPOS native = recpos.GetNativeRecpos();
            int err = NativeMethods.JetGetRecordPosition(sesid.Value, tableid.Value, out native, native.cbStruct);
            recpos.SetFromNativeRecpos(native);
            return err;
        }

        public int JetGotoPosition(JET_SESID sesid, JET_TABLEID tableid, JET_RECPOS recpos)
        {
            this.TraceFunctionCall("JetGotoRecordPosition");
            NATIVE_RECPOS native = recpos.GetNativeRecpos();
            return NativeMethods.JetGotoPosition(sesid.Value, tableid.Value, ref native);
        }

        #endregion

        #region DML

        public int JetDelete(JET_SESID sesid, JET_TABLEID tableid)
        {
            this.TraceFunctionCall("JetDelete");
            return this.Err(NativeMethods.JetDelete(sesid.Value, tableid.Value));
        }

        public int JetPrepareUpdate(JET_SESID sesid, JET_TABLEID tableid, JET_prep prep)
        {
            this.TraceFunctionCall("JetPrepareUpdate");
            return this.Err(NativeMethods.JetPrepareUpdate(sesid.Value, tableid.Value, (uint)prep));
        }

        public int JetUpdate(JET_SESID sesid, JET_TABLEID tableid, byte[] bookmark, int bookmarkSize, out int actualBookmarkSize)
        {
            this.TraceFunctionCall("JetUpdate");
            this.CheckDataSize(bookmark, bookmarkSize, "bookmarkSize");

            uint cbActual;
            int err = this.Err(NativeMethods.JetUpdate(sesid.Value, tableid.Value, bookmark, (uint)bookmarkSize, out cbActual));
            actualBookmarkSize = (int)cbActual;
            return err;
        }

        public int JetSetColumn(JET_SESID sesid, JET_TABLEID tableid, JET_COLUMNID columnid, IntPtr data, int dataSize, SetColumnGrbit grbit, JET_SETINFO setinfo)
        {
            this.TraceFunctionCall("JetSetColumn");
            this.CheckNotNegative(dataSize, "dataSize");
            if (IntPtr.Zero == data)
            {
                if (dataSize > 0 && (SetColumnGrbit.SizeLV != (grbit & SetColumnGrbit.SizeLV)))
                {
                    throw new ArgumentOutOfRangeException(
                        "dataSize",
                        dataSize,
                        "cannot be greater than the length of the data (unless the SizeLV option is used)");
                }
            }

            if (null != setinfo)
            {
                NATIVE_SETINFO nativeSetinfo = setinfo.GetNativeSetinfo();
                return this.Err(NativeMethods.JetSetColumn(sesid.Value, tableid.Value, columnid.Value, data, (uint)dataSize, (uint)grbit, ref nativeSetinfo));
            }

            return this.Err(NativeMethods.JetSetColumn(sesid.Value, tableid.Value, columnid.Value, data, (uint)dataSize, (uint)grbit, IntPtr.Zero));
        }

        public int JetGetLock(JET_SESID sesid, JET_TABLEID tableid, GetLockGrbit grbit)
        {
            this.TraceFunctionCall("JetGetLock");
            return this.Err(NativeMethods.JetGetLock(sesid.Value, tableid.Value, (uint)grbit));
        }

        /// <summary>
        /// Performs an atomic addition operation on one column. This function allows
        /// multiple sessions to update the same record concurrently without conflicts.
        /// </summary>
        /// <param name="sesid">The session to use.</param>
        /// <param name="tableid">The cursor to update.</param>
        /// <param name="columnid">
        /// The column to update. This must be an escrow updatable column.
        /// </param>
        /// <param name="delta">The buffer containing the addend.</param>
        /// <param name="deltaSize">The size of the addend.</param>
        /// <param name="previousValue">
        /// An output buffer that will recieve the current value of the column. This buffer
        /// can be null.
        /// </param>
        /// <param name="previousValueLength">The size of the previousValue buffer.</param>
        /// <param name="actualPreviousValueLength">Returns the actual size of the previousValue.</param>
        /// <param name="grbit">Escrow update options.</param>
        /// <returns>An error code if the operation fails.</returns>
        public int JetEscrowUpdate(
            JET_SESID sesid,
            JET_TABLEID tableid,
            JET_COLUMNID columnid,
            byte[] delta,
            int deltaSize,
            byte[] previousValue,
            int previousValueLength,
            out int actualPreviousValueLength,
            EscrowUpdateGrbit grbit)
        {
            this.TraceFunctionCall("JetEscrowUpdate");
            this.CheckNotNull(delta, "delta");
            this.CheckDataSize(delta, deltaSize, "deltaSize");
            this.CheckDataSize(previousValue, previousValueLength, "previousValueLength");

            uint cbOldActual;
            int err = this.Err(NativeMethods.JetEscrowUpdate(
                                  sesid.Value,
                                  tableid.Value,
                                  columnid.Value,
                                  delta,
                                  (uint)deltaSize,
                                  previousValue,
                                  (uint)previousValueLength,
                                  out cbOldActual,
                                  (uint)grbit));
            actualPreviousValueLength = (int)cbOldActual;
            return err;
        }

        #endregion

        #region Helper Methods

        /// <summary>
        /// Make an array of native columndefs from JET_COLUMNDEFs.
        /// </summary>
        /// <param name="columns">Columndefs to convert.</param>
        /// <param name="numColumns">Number of columndefs to convert.</param>
        /// <returns>An array of native columndefs.</returns>
        private static NATIVE_COLUMNDEF[] GetNativecolumndefs(JET_COLUMNDEF[] columns, int numColumns)
        {
            var nativecolumndefs = new NATIVE_COLUMNDEF[numColumns];
            for (int i = 0; i < numColumns; ++i)
            {
                nativecolumndefs[i] = columns[i].GetNativeColumndef();
            }

            return nativecolumndefs;
        }

        /// <summary>
        /// Set managed columnids from unmanaged columnids.
        /// </summary>
        /// <param name="columnids">The columnids to set.</param>
        /// <param name="nativecolumnids">The native columnids.</param>
        /// <param name="numColumns">The number of columnids to set.</param>
        private static void SetColumnids(JET_COLUMNID[] columnids, uint[] nativecolumnids, int numColumns)
        {
            for (int i = 0; i < numColumns; ++i)
            {
                columnids[i] = new JET_COLUMNID { Value = nativecolumnids[i] };
            }
        }

        /// <summary>
        /// Creates indexes over data in an ESE database.
        /// </summary>
        /// <param name="sesid">The session to use.</param>
        /// <param name="tableid">The table to create the index on.</param>
        /// <param name="indexcreates">Array of objects describing the indexes to be created.</param>
        /// <param name="numIndexCreates">Number of index description objects.</param>
        /// <returns>An error code.</returns>
        private int CreateIndexes(JET_SESID sesid, JET_TABLEID tableid, JET_INDEXCREATE[] indexcreates, int numIndexCreates)
        {
            var nativeIndexcreates = new NATIVE_INDEXCREATE[indexcreates.Length];
            for (int i = 0; i < numIndexCreates; ++i)
            {
                nativeIndexcreates[i] = indexcreates[i].GetNativeIndexcreate();
            }

            // pin the memory
            using (var handles = new GCHandleCollection())
            {
                for (int i = 0; i < numIndexCreates; ++i)
                {
                    if (null != indexcreates[i].pidxUnicode)
                    {
                        NATIVE_UNICODEINDEX unicode = indexcreates[i].pidxUnicode.GetNativeUnicodeIndex();
                        nativeIndexcreates[i].pidxUnicode = handles.Add(unicode);
                        nativeIndexcreates[i].grbit |= (uint)VistaGrbits.IndexUnicode;
                    }
                    ////nativeIndexcreates[i].rgconditionalcolumn = handles.Add(indexcreates[i].rgconditionalcolumn);
                }

                return
                    this.Err(
                        NativeMethods.JetCreateIndex2(sesid.Value, tableid.Value, nativeIndexcreates, (uint)numIndexCreates));
            }
        }

        /// <summary>
        /// Creates indexes over data in an ESE database.
        /// </summary>
        /// <param name="sesid">The session to use.</param>
        /// <param name="tableid">The table to create the index on.</param>
        /// <param name="indexcreates">Array of objects describing the indexes to be created.</param>
        /// <param name="numIndexCreates">Number of index description objects.</param>
        /// <returns>An error code.</returns>
        private int CreateIndexes2(JET_SESID sesid, JET_TABLEID tableid, JET_INDEXCREATE[] indexcreates, int numIndexCreates)
        {
            var nativeIndexcreates = new NATIVE_INDEXCREATE2[indexcreates.Length];
            for (int i = 0; i < numIndexCreates; ++i)
            {
                nativeIndexcreates[i] = indexcreates[i].GetNativeIndexcreate2();
            }

            // pin the memory
            using (var handles = new GCHandleCollection())
            {
                for (int i = 0; i < numIndexCreates; ++i)
                {
                    if (null != indexcreates[i].pidxUnicode)
                    {
                        NATIVE_UNICODEINDEX unicode = indexcreates[i].pidxUnicode.GetNativeUnicodeIndex();
                        nativeIndexcreates[i].indexcreate.pidxUnicode = handles.Add(unicode);
                        nativeIndexcreates[i].indexcreate.grbit |= (uint)VistaGrbits.IndexUnicode;
                    }
                    ////nativeIndexcreates[i].rgconditionalcolumn = handles.Add(indexcreates[i].rgconditionalcolumn);
                }

                return
                    this.Err(
                        NativeMethods.JetCreateIndex2(sesid.Value, tableid.Value, nativeIndexcreates, (uint)numIndexCreates));
            }
        }

        /// <summary>
        /// Calculates the capabilities of the current Esent version.
        /// </summary>
        private void DetermineCapabilities()
        {
            // Create new capabilities, set as all false. This will allow
            // us to call into Esent.
            this.Capabilities = new JetCapabilities();
            this.Capabilities.ColumnsKeyMost = 12;  // XP through Vista

            var version = (uint)this.GetVersionFromEsent();
            var buildNumber = (int)((version & 0xFFFFFF) >> 8);

            Trace.WriteLineIf(
                this.traceSwitch.TraceVerbose,
                String.Format("Version = {0}, BuildNumber = {1}", version, buildNumber));

            if (buildNumber >= 2700)
            {
                Trace.WriteLineIf(this.traceSwitch.TraceVerbose, "Supports Server 2003 features");
                this.Capabilities.SupportsServer2003Features = true;
            }

            if (buildNumber >= 6000)
            {
                Trace.WriteLineIf(this.traceSwitch.TraceVerbose, "Supports Vista features");
                this.Capabilities.SupportsVistaFeatures = true;
                Trace.WriteLineIf(this.traceSwitch.TraceVerbose, "Supports Unicode paths");
                this.Capabilities.SupportsUnicodePaths = true;
                Trace.WriteLineIf(this.traceSwitch.TraceVerbose, "Supports large keys");
                this.Capabilities.SupportsLargeKeys = true;
                Trace.WriteLineIf(this.traceSwitch.TraceVerbose, "Supports 16-column keys");
                this.Capabilities.ColumnsKeyMost = 16;
            }

            if (buildNumber >= 7000)
            {
                Trace.WriteLineIf(this.traceSwitch.TraceVerbose, "Supports Windows 7 features");
                this.Capabilities.SupportsWindows7Features = true;
            }
        }

        /// <summary>
        /// Create an instance and get the current version of Esent.
        /// </summary>
        /// <returns>The current version of Esent.</returns>
        private uint GetVersionFromEsent()
        {
            JET_INSTANCE instance;
            this.JetCreateInstance(out instance, "GettingEsentVersion");
            try
            {
                this.JetSetSystemParameter(instance, JET_SESID.Nil, JET_param.Recovery, 0, "off");
                this.JetSetSystemParameter(instance, JET_SESID.Nil, JET_param.NoInformationEvent, 1, null);
                this.JetSetSystemParameter(instance, JET_SESID.Nil, JET_param.MaxTemporaryTables, 0, null);
                this.JetInit(ref instance);

                JET_SESID sesid;
                this.JetBeginSession(instance, out sesid, String.Empty, String.Empty);
                try
                {
                    uint version;
                    this.JetGetVersion(sesid, out version);
                    return version;
                }
                finally
                {
                    this.JetEndSession(sesid, EndSessionGrbit.None);
                }
            }
            finally
            {
                this.JetTerm(instance);
            }
        }

        #endregion

        #region Parameter Checking and Tracing

        /// <summary>
        /// Make sure the data and dataSize arguments match.
        /// </summary>
        /// <param name="data">The data buffer.</param>
        /// <param name="dataSize">The size of the data.</param>
        /// <param name="argumentName">The name of the size argument.</param>
        /// <typeparam name="T">The type of the data.</typeparam>
        private void CheckDataSize<T>(ICollection<T> data, int dataSize, string argumentName)
        {
            this.CheckNotNegative(dataSize, argumentName);
            if ((null == data && 0 != dataSize) || (null != data && dataSize > data.Count))
            {
                Trace.WriteLineIf(this.traceSwitch.TraceError, "CheckDataSize failed");
                throw new ArgumentOutOfRangeException(
                    argumentName,
                    dataSize,
                    "cannot be greater than the length of the buffer");
            }
        }

        /// <summary>
        /// Make sure the given object isn't null. If it is
        /// then throw an ArgumentNullException.
        /// </summary>
        /// <param name="o">The object to check.</param>
        /// <param name="paramName">The name of the parameter.</param>
        private void CheckNotNull(object o, string paramName)
        {
            if (null == o)
            {
                Trace.WriteLineIf(this.traceSwitch.TraceError, "CheckNotNull failed");
                throw new ArgumentNullException(paramName);
            }
        }

        /// <summary>
        /// Make sure the given integer isn't negative. If it is
        /// then throw an ArgumentOutOfRangeException.
        /// </summary>
        /// <param name="i">The integer to check.</param>
        /// <param name="paramName">The name of the parameter.</param>
        private void CheckNotNegative(int i, string paramName)
        {
            if (i < 0)
            {
                Trace.WriteLineIf(this.traceSwitch.TraceError, "CheckNotNegative failed");
                throw new ArgumentOutOfRangeException(paramName, i, "cannot be negative");
            }
        }

        /// <summary>
        /// Trace a call to an ESENT function.
        /// </summary>
        /// <param name="function">The name of the function being called.</param>
        [Conditional("TRACE")]
        private void TraceFunctionCall(string function)
        {
            Trace.WriteLineIf(this.traceSwitch.TraceInfo, function);
        }

        /// <summary>
        /// Can be used to trap ESENT errors.
        /// </summary>
        /// <param name="err">The error being returned.</param>
        /// <returns>The error.</returns>
        private int Err(int err)
        {
            if (0 == err)
            {
                Trace.WriteLineIf(this.traceSwitch.TraceVerbose, "JET_err.Success");
            }
            else if (err > 0)
            {
                Trace.WriteLineIf(this.traceSwitch.TraceWarning, (JET_wrn)err);
            }
            else
            {
                Trace.WriteLineIf(this.traceSwitch.TraceError, (JET_err)err);
            }

            return err;
        }

        #endregion Parameter Checking and Tracing
    }
}