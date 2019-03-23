using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace Brokeree.WepApi.Helpers
{
    using System;
    using System.Collections;
    using System.Collections.Generic;
    using System.Data;
    using System.Data.SqlClient;
    using System.Text;
    using System.Xml;

    namespace PayPoint.Core.Db
    {
        public class Mssql : IDisposable
        {
            static readonly int DefaultCommandTimeout = new SqlCommand().CommandTimeout;

            #region Свойства
            readonly string ConnectionString;

            static Hashtable htUpdatingDA = new Hashtable();
            static Hashtable htParameters = new Hashtable();
            static Hashtable htConstraint = new Hashtable();
            static Dictionary<int, object[]> DErrors = new Dictionary<int, object[]>();

            SqlConnection conn;
            SqlTransaction tran;
            enum CommandAction { Execute, Open, GetXml, Scalar }

            // TODO: Полностью скрыть это свойство, оно должно управляться только через WSShablon вместе с Server.ScriptTimeOut
            public int CommandTimeout = DefaultCommandTimeout;
            #endregion Свойства

            #region Конструкторы и деструкторы
            /// <summary>Создает экземпляр MSSQL2000</summary>
            /// <param name="connectionString">Строка подключения к серверу БД</param>
            public Mssql(string connectionString)
            {
                if (String.IsNullOrEmpty(connectionString))
                    throw new ApplicationException("ConnectionString property is empty");
                ConnectionString = connectionString;
            }

            public void Dispose() { Close(); }
            #endregion Конструкторы и деструкторы

            #region Приватные методы
            void ClearConnection()
            {
                if (conn != null)
                    conn.Dispose();
                conn = null;
            }
            void ClearTransaction()
            {
                if (tran != null)
                    tran.Dispose();
                tran = null;
            }

            SqlCommand PrepareCommand(string cmdText, CommandType cmdType, SqlParameter[] cmdParams)
            {
#if (DEBUG)
                if ((cmdType != CommandType.StoredProcedure) && (cmdType != CommandType.Text))
                    throw new ArgumentOutOfRangeException("cmdType");
#endif
                SqlCommand cmd = new SqlCommand(cmdText, conn, tran);
                cmd.CommandType = cmdType;
                cmd.CommandTimeout = CommandTimeout;

                if (cmdParams != null)
                    foreach (SqlParameter Param in cmdParams)
                        if (Param != null)
                        {
                            if (Param.Value == null)
                            {
                                Param.Value = DBNull.Value;
                                if ((Param.Direction == ParameterDirection.Output) && (Param.Size == 0))
                                    switch (Param.DbType)
                                    {
                                        case DbType.AnsiString:
                                        case DbType.AnsiStringFixedLength:
                                        case DbType.Binary:
                                        case DbType.String:
                                        case DbType.StringFixedLength:
                                        case DbType.Xml:
                                            Param.Size = -1;
                                            break;
                                    }
                            }
                            cmd.Parameters.Add(Param);
                        }
                return cmd;
            }

            object DoCommand(string cmdText, CommandType cmdType, CommandAction cmdAction, SqlParameter[] cmdParams, out string Err)
            {
                try
                {
                    Err = string.Empty;
                    return DoCommand(cmdText, cmdType, cmdAction, cmdParams);
                }
                catch (Exception E)
                {
                    Err = E.Message;
                    return null;
                }
            }
            object DoCommand(string cmdText, CommandType cmdType, CommandAction cmdAction, SqlParameter[] cmdParams)
            {
                SqlCommand cmd = PrepareCommand(cmdText, cmdType, cmdParams);
                try
                {
                    switch (cmdAction)
                    {
                        case CommandAction.Scalar:
                            return cmd.ExecuteScalar();
                        case CommandAction.Execute:
                            cmd.ExecuteNonQuery();
                            return null;
                        case CommandAction.Open:
                            DataSet ds = new DataSet();
                            SqlDataAdapter da = new SqlDataAdapter(cmd);
                            // da.MissingSchemaAction = MissingSchemaAction.Add;
                            da.Fill(ds);
                            return ds;
                        case CommandAction.GetXml:
                            using (XmlReader xr = cmd.ExecuteXmlReader())
                            {
                                xr.MoveToContent();
                                StringBuilder sb = new StringBuilder();
                                while (!xr.EOF)
                                    sb.Append(xr.ReadOuterXml());
                                return sb.ToString();
                            }
                        default: throw new ArgumentOutOfRangeException("cmdAction");
                    }
                }
                catch (Exception e)
                {
                    if (e is SqlException)
                        throw (SqlException)e;
                    else
                    {
                        Close();
                        throw;
                    }
                }
            }

            static SqlDbType SqlType(string Type)
            {
                switch (Type.ToLower())
                {
                    case "char": return SqlDbType.Char;
                    case "nchar": return SqlDbType.NChar;
                    case "varchar": return SqlDbType.VarChar;
                    case "nvarchar": return SqlDbType.NVarChar;

                    case "bit": return SqlDbType.Bit;
                    case "tinyint": return SqlDbType.TinyInt;
                    case "smallint": return SqlDbType.SmallInt;
                    case "int": return SqlDbType.Int;
                    case "bigint": return SqlDbType.BigInt;
                    case "timestamp": return SqlDbType.Timestamp;

                    case "real": return SqlDbType.Real;
                    case "float": return SqlDbType.Float;
                    case "decimal": return SqlDbType.Decimal;
                    case "numeric": return SqlDbType.Decimal;
                    case "smallmoney": return SqlDbType.SmallMoney;
                    case "money": return SqlDbType.Money;

                    case "time": return SqlDbType.Time;
                    case "date": return SqlDbType.Date;
                    case "smalldatetime": return SqlDbType.SmallDateTime;
                    case "datetime": return SqlDbType.DateTime;
                    case "datetime2": return SqlDbType.DateTime2;
                    case "datetimeoffset": return SqlDbType.DateTimeOffset;

                    case "binary": return SqlDbType.Binary;
                    case "varbinary": return SqlDbType.VarBinary;
                    case "image": return SqlDbType.Image;
                    case "text": return SqlDbType.Text;
                    case "ntext": return SqlDbType.NText;
                    case "uniqueidentifier": return SqlDbType.UniqueIdentifier;
                    case "xml": return SqlDbType.Xml;
                    case "sql_variant": return SqlDbType.Variant;
                    default: return SqlDbType.VarChar;
                }
            }
            static SqlParameter SqlParamDefine(SqlParameter Param, short max_length, byte precision, byte scale)
            {
                switch (Param.SqlDbType)
                {
                    case SqlDbType.Xml:
                    case SqlDbType.Char:
                    case SqlDbType.VarChar:
                    case SqlDbType.Binary:
                    case SqlDbType.VarBinary:
                        Param.Size = max_length;
                        break;
                    case SqlDbType.NChar:
                    case SqlDbType.NVarChar:
                        Param.Size = (max_length > 0) ? max_length / 2 : max_length;
                        break;
                    case SqlDbType.Decimal:
                        Param.Precision = precision;
                        Param.Scale = scale;
                        break;
                }
                return Param;
            }
            IEnumerable<SqlParameter> ObjectParams(string Object)
            {
                SqlParameter[] Params; string htKey = string.Format("{0}.{1}", DataBase, Object);
                lock (htParameters.SyncRoot)
                    Params = (SqlParameter[])htParameters[htKey];
                if (Params == null)
                {
                    DataTable dt = SQLTextOpen(@"
SELECT	 Replace(P.[name],'@','')	AS [Name]
	,P.[name]			AS [Parameter]
	,T.[name]			AS [Type]
	,P.max_length
	,P.[precision]
	,P.scale
	,P.[is_output]			AS [IsOut]
FROM	     sys.parameters	P
	JOIN sys.types		T ON T.user_type_id = P.user_type_id
WHERE	P.object_id = Object_ID(@@Object)
ORDER BY P.parameter_id", new SqlParameter[] { new SqlParameter("@@Object", Object) }).Tables[0];
                    if (dt.Rows.Count == 0)
                        throw new Exception(string.Format("Объект '{0}' не найден в базе '{1}'!", Object, DataBase));
                    DataColumn
                          dcName = dt.Columns["Name"]
                        , dcParameter = dt.Columns["Parameter"]
                        , dcType = dt.Columns["Type"]
                        , dcMaxLength = dt.Columns["max_length"]
                        , dcPrecision = dt.Columns["precision"]
                        , dcScale = dt.Columns["scale"]
                        , dcIsOut = dt.Columns["IsOut"];
                    Params = new SqlParameter[dt.Rows.Count]; int counter = 0;
                    foreach (DataRow dr in dt.Rows)
                    {
                        Params[counter] = SqlParamDefine(new SqlParameter((string)dr[dcParameter], SqlType((string)dr[dcType])), (short)dr[dcMaxLength], (byte)dr[dcPrecision], (byte)dr[dcScale]);
                        Params[counter].SourceColumn = (string)dr[dcName];
                        if ((bool)dr[dcIsOut])
                            Params[counter].Direction = ParameterDirection.InputOutput;
                        counter++;
                    }
#if (!DEBUG)
				lock (htParameters.SyncRoot)
					if (htParameters.ContainsKey(htKey))
						Params = (SqlParameter[])htParameters[htKey];
					else
						htParameters.Add(htKey, Params);
#endif
                }
                // Return clones params
                foreach (SqlParameter Param in Params)
                    yield return (SqlParameter)((ICloneable)Param).Clone();
            }
            #endregion Приватные методы

            #region Методы для работы с коннектом
            /// <summary>Подключиться к серверу БД</summary>
            public void Open()
            {
                if (conn == null)
                    conn = new SqlConnection();

                if (conn.State == ConnectionState.Broken)
                    conn.Close();

                if (conn.State != ConnectionState.Closed) return;

                conn.ConnectionString = this.ConnectionString;
                conn.Open();
            }

            /// <summary>Закрыть соединение с сервером БД</summary>
            public void Close()
            {
                tran = null;
                conn?.Close();
                conn = null;
            }
            public bool ConnectionOpened => (conn != null) && (conn.State == ConnectionState.Open);

            /// <summary>Активная БД</summary>
            public string DataBase
            {
                get { return (string)SQLScalar("SELECT DB_Name()"); }
                set { SQLTextExec($"USE [{value}]"); }
            }
            #endregion Методы для работы с коннектом

            #region Методы для работы с транзакцией
            /// <summary>Открыть транзакцию</summary>
            public void BeginTransaction() { BeginTransaction(IsolationLevel.Unspecified); }
            /// <summary>Открыть транзакцию</summary>
            /// <param name="Level">Уровень изоляции транзакции</param>
            public void BeginTransaction(IsolationLevel Level)
            {
                if (conn == null)
                    throw new Exception("Connection has not been opened.");
                if (TransactionStarted)
                    throw new Exception("Transaction has been started.");
                else
                    tran = conn.BeginTransaction(Level);
            }
            /// <summary>Закрыть транзакцию</summary>
            public void CommitTransaction()
            {
                if (TransactionStarted)
                    tran.Commit();
                else
                    throw new Exception("Transaction has not been started.");
                ClearTransaction();
            }
            /// <summary>Откатить транзакцию</summary>
            public void RollbackTransaction()
            {
                if (TransactionStarted)
                    tran.Rollback();
                else
                    throw new Exception("Transaction has not been started.");
                ClearTransaction();
            }
            /// <summary>Транзакция открыта</summary>
            public bool TransactionStarted => tran != null;

            #endregion Методы для работы с транзакцией

            #region Методы для работы с параметрами
            // TODO: Remove use methodods StoredProcOpen/StoredProcOpen directly
            /// <summary>Создает массив SqlParameter-ов и заполняет SqlParameter'ы значениями</summary>
            /// <param name="StoredProcName">Имя хранимой процедуры</param>
            /// <param name="StoredProcParamsValues">массив значений для параметров хранимой процедуры</param>
            SqlParameter[] CreateStoredProcParams(string StoredProcName, object[] ParamsValues)
            {
                if ((ParamsValues == null) || ParamsValues.Length == 0)
                    return null;
                List<SqlParameter> lParams = new List<SqlParameter>(ParamsValues.Length); int counter = 0;
                foreach (SqlParameter Param in ObjectParams(StoredProcName))
                {
                    object Value = ParamsValues[counter++];
                    if (Value != null)
                    {
                        Param.Value = Value;
                        lParams.Add(Param);
                    }
                    if (counter >= ParamsValues.Length)
                        break;
                }
                return lParams.ToArray();
            }
            #endregion Методы для работы с параметрами

            #region Основные методы (извлечение данных, выполнение SP, и т.д)
            #region StoredProc
            /// <summary>Выполнение хранимой процедуры</summary>
            /// <param name="StoredProcName">Имя хранимой процедуры</param>
            public void StoredProcExec(string StoredProcName) { DoCommand(StoredProcName, CommandType.StoredProcedure, CommandAction.Execute, null); }
            /// <summary>Выполнение хранимой процедуры</summary>
            /// <param name="StoredProcName">Имя хранимой процедуры</param>
            /// <param name="StoredProcParams">Массив параметров</param>
            public SqlParameter[] StoredProcExec(string StoredProcName, SqlParameter[] StoredProcParams)
            {
                DoCommand(StoredProcName, CommandType.StoredProcedure, CommandAction.Execute, StoredProcParams);
                return StoredProcParams;
            }
            /// <summary>Выполнение хранимой процедуры</summary>
            /// <param name="StoredProcName">Имя хранимой процедуры</param>
            /// <param name="StoredProcParamsValues">Массив значений для параметров хранимой процедуры</param>
            public SqlParameter[] StoredProcExec(string StoredProcName, object[] StoredProcParamsValues)
            {
                SqlParameter[] Params = CreateStoredProcParams(StoredProcName, StoredProcParamsValues);
                DoCommand(StoredProcName, CommandType.StoredProcedure, CommandAction.Execute, Params);
                return Params;
            }
            /// <summary>Выполнение хранимой процедуры и чтение результирующего набора данных</summary>
            /// <param name="StoredProcName">Имя хранимой процедуры</param>
            public DataSet StoredProcOpen(string StoredProcName) { return (DataSet)DoCommand(StoredProcName, CommandType.StoredProcedure, CommandAction.Open, null); }
            /// <summary>Выполнение хранимой процедуры и чтение результирующего набора данных</summary>
            /// <param name="StoredProcName">Имя хранимой процедуры</param>
            /// <param name="StoredProcParams">Массив параметров</param>
            public DataSet StoredProcOpen(string StoredProcName, SqlParameter[] StoredProcParams) { return (DataSet)DoCommand(StoredProcName, CommandType.StoredProcedure, CommandAction.Open, StoredProcParams); }
            /// <summary>Выполнение хранимой процедуры и чтение результирующего набора данных</summary>
            /// <param name="StoredProcName">Имя хранимой процедуры</param>
            /// <param name="StoredProcParamsValues">Массив значений для параметров хранимой процедуры</param>
            public DataSet StoredProcOpen(string StoredProcName, object[] StoredProcParamsValues) { return (DataSet)DoCommand(StoredProcName, CommandType.StoredProcedure, CommandAction.Open, CreateStoredProcParams(StoredProcName, StoredProcParamsValues)); }
            /// <summary>Получение XML'я из хранимой процедуры</summary>
            /// <param name="StoredProcName">Имя хранимой процедуры</param>
            public string StoredProcXml(string StoredProcName) { return (string)DoCommand(StoredProcName, CommandType.StoredProcedure, CommandAction.GetXml, null); }
            /// <summary>Получение XML'я из хранимой процедуры</summary>
            /// <param name="StoredProcName">Имя хранимой процедуры</param>
            /// <param name="StoredProcParams">Массив параметров</param>
            public string StoredProcXml(string StoredProcName, SqlParameter[] StoredProcParams) { return (string)DoCommand(StoredProcName, CommandType.StoredProcedure, CommandAction.GetXml, StoredProcParams); }
            /// <summary>Получение XML'я из хранимой процедуры</summary>
            /// <param name="StoredProcName">Имя хранимой процедуры</param>
            /// <param name="StoredProcParamsValues">Массив значений для параметров хранимой процедуры</param>
            public string StoredProcXml(string StoredProcName, object[] StoredProcParamsValues) { return (string)DoCommand(StoredProcName, CommandType.StoredProcedure, CommandAction.GetXml, CreateStoredProcParams(StoredProcName, StoredProcParamsValues)); }
            #endregion StoredProc

            #region SQLText
            /// <summary>Выполнение SQL-Statament-а</summary>
            /// <param name="SQLText">Текст SQL-Statament-а</param>
            public void SQLTextExec(string SQLText) { DoCommand(SQLText, CommandType.Text, CommandAction.Execute, null); }
            /// <summary>Выполнение SQL-Statament-а</summary>
            /// <param name="SQLText">Текст SQL-Statament-а</param>
            /// <param name="Params">Массив параметров SQL-Statament-а</param>
            public SqlParameter[] SQLTextExec(string SQLText, SqlParameter[] Params)
            {
                DoCommand(SQLText, CommandType.Text, CommandAction.Execute, Params);
                return Params;
            }
            /// <summary>Выполнение SQL-Statament-а и чтение результирующего набора данных</summary>
            /// <param name="SQLText">Текст SQL-Statament-а</param>
            public DataSet SQLTextOpen(string SQLText) { return (DataSet)DoCommand(SQLText, CommandType.Text, CommandAction.Open, null); }
            /// <summary>Выполнение SQL-Statament-а и чтение результирующего набора данных</summary>
            /// <param name="SQLText">Текст SQL-Statament-а</param>
            /// <param name="Params">Массив параметров SQL-Statament-а</param>
            public DataSet SQLTextOpen(string SQLText, SqlParameter[] Params) { return (DataSet)DoCommand(SQLText, CommandType.Text, CommandAction.Open, Params); }
            /// <summary>Получение XML'я из SQL-Statament-а</summary>
            /// <param name="SQLText">Текст SQL-Statament-а</param>
            public string SQLTextXml(string SQLText) { return (string)DoCommand(SQLText, CommandType.Text, CommandAction.GetXml, null); }
            /// <summary>Получение XML'я из SQL-Statament-а</summary>
            /// <param name="SQLText">Текст SQL-Statament-а</param>
            /// <param name="Params">Массив параметров SQL-Statament-а</param>
            public string SQLTextXml(string SQLText, SqlParameter[] Params) { return (string)DoCommand(SQLText, CommandType.Text, CommandAction.GetXml, Params); }
            #endregion SQLText

            #region SQLScalar
            /// <summary>Выполнение SQL-Statament-а и чтение результирующего одного значения</summary>
            /// <param name="SQLText">Текст SQL-Statament-а</param>
            public object SQLScalar(string SQLText)
            {
                return DoCommand(SQLText, CommandType.Text, CommandAction.Scalar, null);
            }
            /// <summary>Выполнение SQL-Statament-а и чтение результирующего одного значения</summary>
            /// <param name="SQLText">Текст SQL-Statament-а</param>
            /// <param name="Params">Массив параметров SQL-Statament-а</param>
            public object SQLScalar(string SQLText, SqlParameter[] Params)
            {
                return DoCommand(SQLText, CommandType.Text, CommandAction.Scalar, Params);
            }
            #endregion SQLScalar
            #endregion Основные методы (извлечение данных, выполнение SP, и т.д)

            #region Статические методы

            // Статические методы должны использоваться только для выполнения одиночных запросов к БД без транзакции
            // в случае когда надо выполнить 2 и более запросов и важна очередность и успех выполнения запросов
            // или требуется выполнение запросов в транзакции,
            // тогда надо создать экземпляр класса MSSQL2000 и использовать его нестатические методы и свойства.

            // TODO: Remove methods with Err

            public static DataSet StoredProcOpen(string ConnectionString, string StoredProcName, SqlParameter[] StoredProcParams, out string Err) { return StoredProcOpen(ConnectionString, DefaultCommandTimeout, StoredProcName, StoredProcParams, out Err); }
            static DataSet StoredProcOpen(string ConnectionString, int CommandTimeout, string StoredProcName, SqlParameter[] StoredProcParams, out string Err)
            {
                Err = string.Empty;
                Mssql db = new Mssql(ConnectionString);
                try
                {
                    db.Open();
                    db.CommandTimeout = CommandTimeout;
                    return db.StoredProcOpen(StoredProcName, StoredProcParams);
                }
                catch (Exception e) { Err = e.Message; return null; }
                finally { db.Close(); }
            }
            public static DataSet StoredProcOpen(string ConnectionString, string StoredProcName, object[] StoredProcParamsValues, out string Err) { return StoredProcOpen(ConnectionString, DefaultCommandTimeout, StoredProcName, StoredProcParamsValues, out Err); }
            static DataSet StoredProcOpen(string ConnectionString, int CommandTimeout, string StoredProcName, object[] StoredProcParamsValues, out string Err)
            {
                Err = string.Empty;
                Mssql db = new Mssql(ConnectionString);
                try
                {
                    db.Open();
                    db.CommandTimeout = CommandTimeout;
                    return db.StoredProcOpen(StoredProcName, StoredProcParamsValues);
                }
                catch (Exception e) { Err = e.Message; return null; }
                finally { db.Close(); }
            }

            public static DataSet SQLTextOpen(string ConnectionString, string SQLText, out string Err) { return SQLTextOpen(ConnectionString, DefaultCommandTimeout, SQLText, new SqlParameter[] { }, out Err); }
            static DataSet SQLTextOpen(string ConnectionString, int CommandTimeout, string SQLText, SqlParameter[] Params, out string Err)
            {
                Err = string.Empty;
                Mssql db = new Mssql(ConnectionString);
                try
                {
                    db.Open();
                    db.CommandTimeout = CommandTimeout;
                    return db.SQLTextOpen(SQLText, Params);
                }
                catch (Exception e) { Err = e.Message; return null; }
                finally { db.Close(); }
            }

            // Методы без Err

            public static void StoredProcExec(string ConnectionString, string StoredProcName) { StoredProcExec(ConnectionString, DefaultCommandTimeout, StoredProcName, new SqlParameter[] { }); }
            public static void StoredProcExec(string ConnectionString, int CommandTimeout, string StoredProcName) { StoredProcExec(ConnectionString, CommandTimeout, StoredProcName, new SqlParameter[] { }); }
            public static SqlParameter[] StoredProcExec(string ConnectionString, string StoredProcName, SqlParameter[] StoredProcParams) { return StoredProcExec(ConnectionString, DefaultCommandTimeout, StoredProcName, StoredProcParams); }
            public static SqlParameter[] StoredProcExec(string ConnectionString, int CommandTimeout, string StoredProcName, SqlParameter[] StoredProcParams)
            {
                using (Mssql db = new Mssql(ConnectionString))
                {
                    db.Open();
                    db.CommandTimeout = CommandTimeout;
                    return db.StoredProcExec(StoredProcName, StoredProcParams);
                }
            }
            public static SqlParameter[] StoredProcExec(string ConnectionString, string StoredProcName, object[] StoredProcParamsValues) { return StoredProcExec(ConnectionString, DefaultCommandTimeout, StoredProcName, StoredProcParamsValues); }
            public static SqlParameter[] StoredProcExec(string ConnectionString, int CommandTimeout, string StoredProcName, object[] StoredProcParamsValues)
            {
                using (Mssql db = new Mssql(ConnectionString))
                {
                    db.Open();
                    db.CommandTimeout = CommandTimeout;
                    return db.StoredProcExec(StoredProcName, StoredProcParamsValues);
                }
            }

            public static DataSet StoredProcOpen(string ConnectionString, string StoredProcName) { return StoredProcOpen(ConnectionString, DefaultCommandTimeout, StoredProcName, new SqlParameter[] { }); }
            public static DataSet StoredProcOpen(string ConnectionString, int CommandTimeout, string StoredProcName) { return StoredProcOpen(ConnectionString, CommandTimeout, StoredProcName, new SqlParameter[] { }); }
            public static DataSet StoredProcOpen(string ConnectionString, string StoredProcName, SqlParameter[] StoredProcParams) { return StoredProcOpen(ConnectionString, DefaultCommandTimeout, StoredProcName, StoredProcParams); }
            public static DataSet StoredProcOpen(string ConnectionString, int CommandTimeout, string StoredProcName, SqlParameter[] StoredProcParams)
            {
                using (Mssql db = new Mssql(ConnectionString))
                {
                    db.Open();
                    db.CommandTimeout = CommandTimeout;
                    return db.StoredProcOpen(StoredProcName, StoredProcParams);
                }
            }
            public static DataSet StoredProcOpen(string ConnectionString, string StoredProcName, object[] StoredProcParamsValues) { return StoredProcOpen(ConnectionString, DefaultCommandTimeout, StoredProcName, StoredProcParamsValues); }
            public static DataSet StoredProcOpen(string ConnectionString, int CommandTimeout, string StoredProcName, object[] StoredProcParamsValues)
            {
                using (Mssql db = new Mssql(ConnectionString))
                {
                    db.Open();
                    db.CommandTimeout = CommandTimeout;
                    return db.StoredProcOpen(StoredProcName, StoredProcParamsValues);
                }
            }

            public static void SQLTextExec(string ConnectionString, string SQLText) { SQLTextExec(ConnectionString, DefaultCommandTimeout, SQLText, new SqlParameter[] { }); }
            public static void SQLTextExec(string ConnectionString, int CommandTimeout, string SQLText) { SQLTextExec(ConnectionString, CommandTimeout, SQLText, new SqlParameter[] { }); }
            public static void SQLTextExec(string ConnectionString, string SQLText, SqlParameter[] Params) { SQLTextExec(ConnectionString, DefaultCommandTimeout, SQLText, Params); }
            public static void SQLTextExec(string ConnectionString, int CommandTimeout, string SQLText, SqlParameter[] Params)
            {
                using (Mssql db = new Mssql(ConnectionString))
                {
                    db.Open();
                    db.CommandTimeout = CommandTimeout;
                    db.SQLTextExec(SQLText, Params);
                }
            }

            public static DataSet SQLTextOpen(string ConnectionString, string SQLText) { return SQLTextOpen(ConnectionString, DefaultCommandTimeout, SQLText, new SqlParameter[] { }); }
            public static DataSet SQLTextOpen(string ConnectionString, int CommandTimeout, string SQLText) { return SQLTextOpen(ConnectionString, CommandTimeout, SQLText, new SqlParameter[] { }); }
            public static DataSet SQLTextOpen(string ConnectionString, string SQLText, SqlParameter[] Params) { return SQLTextOpen(ConnectionString, DefaultCommandTimeout, SQLText, Params); }
            public static DataSet SQLTextOpen(string ConnectionString, int CommandTimeout, string SQLText, SqlParameter[] Params)
            {
                using (Mssql db = new Mssql(ConnectionString))
                {
                    db.Open();
                    db.CommandTimeout = CommandTimeout;
                    return db.SQLTextOpen(SQLText, Params);
                }
            }

            public static object SQLScalar(string ConnectionString, string SQLText) { return SQLScalar(ConnectionString, DefaultCommandTimeout, SQLText, new SqlParameter[] { }); }
            public static object SQLScalar(string ConnectionString, int CommandTimeout, string SQLText) { return SQLScalar(ConnectionString, CommandTimeout, SQLText, new SqlParameter[] { }); }
            public static object SQLScalar(string ConnectionString, string SQLText, SqlParameter[] Params) { return SQLScalar(ConnectionString, DefaultCommandTimeout, SQLText, Params); }
            public static object SQLScalar(string ConnectionString, int CommandTimeout, string SQLText, SqlParameter[] Params)
            {
                using (Mssql db = new Mssql(ConnectionString))
                {
                    db.Open();
                    db.CommandTimeout = CommandTimeout;
                    return db.SQLScalar(SQLText, Params);
                }
            }
            #endregion Статические методы
        }
    }

}