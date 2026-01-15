'
' DotNetNuke® - http://www.dotnetnuke.com
' Copyright (c) 2002-2008
' by DotNetNuke Corporation
'
' Permission is hereby granted, free of charge, to any person obtaining a copy of this software and associated 
' documentation files (the "Software"), to deal in the Software without restriction, including without limitation 
' the rights to use, copy, modify, merge, publish, distribute, sublicense, and/or sell copies of the Software, and 
' to permit persons to whom the Software is furnished to do so, subject to the following conditions:
'
' The above copyright notice and this permission notice shall be included in all copies or substantial portions 
' of the Software.
'
' THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED 
' TO THE WARRANTIES OF MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL 
' THE AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION OF 
' CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER 
' DEALINGS IN THE SOFTWARE.
'

Imports System
Imports System.Data
Imports System.Data.Common
Imports System.Data.SqlClient
Imports System.Data.SqlTypes
Imports Microsoft.ApplicationBlocks.Data
Imports System.IO
Imports System.Text
Imports System.Web
Imports DotNetNuke
Imports DotNetNuke.Common.Utilities
Imports DotNetNuke.Framework.Providers
Imports DotNetNuke.Entities.Users

Namespace DotNetNuke.Data

    Public Class SqlDataProvider
        Inherits DataProvider

#Region "Private Members"
        Private Const ProviderType As String = "data"

        Private _providerConfiguration As ProviderConfiguration = ProviderConfiguration.GetProviderConfiguration(ProviderType)
        Private _connectionString As String
        Private _providerPath As String
        Private _objectQualifier As String
        Private _databaseOwner As String
        Private _upgradeConnectionString As String
#End Region

#Region "Constructors"

        Public Sub New()

            ' Read the configuration specific information for this provider
            Dim objProvider As Provider = CType(_providerConfiguration.Providers(_providerConfiguration.DefaultProvider), Provider)

            ' Read the attributes for this provider

            'Get Connection string from web.config
            _connectionString = Config.GetConnectionString()

            If _connectionString = "" Then
                ' Use connection string specified in provider
                _connectionString = objProvider.Attributes("connectionString")
            End If

            If Convert.ToString(objProvider.Attributes("upgradeConnectionString")) <> "" Then
                _upgradeConnectionString = objProvider.Attributes("upgradeConnectionString")
            Else
                _upgradeConnectionString = _connectionString
            End If

            _providerPath = Config.GetProviderPath(ProviderType)
            _objectQualifier = Config.GetObjectQualifer()
            _databaseOwner = Config.GetDataBaseOwner()

        End Sub

#End Region

#Region "Public Properties"

        Public ReadOnly Property ConnectionString() As String
            Get
                Return _connectionString
            End Get
        End Property

        Public ReadOnly Property ProviderPath() As String
            Get
                Return _providerPath
            End Get
        End Property

        Public ReadOnly Property ObjectQualifier() As String
            Get
                Return _objectQualifier
            End Get
        End Property

        Public ReadOnly Property DatabaseOwner() As String
            Get
                Return _databaseOwner
            End Get
        End Property

        Public ReadOnly Property UpgradeConnectionString() As String
            Get
                Return _upgradeConnectionString
            End Get
        End Property

#End Region

#Region "Private Methods"

        Private Sub ExecuteADOScript(ByVal trans As SqlTransaction, ByVal SQL As String)

            'Get the connection
            Dim connection As SqlConnection = trans.Connection

            'Create a new command (with no timeout)
            Dim command As New SqlCommand(SQL, trans.Connection)
            command.Transaction = trans
            command.CommandTimeout = 0

            command.ExecuteNonQuery()

        End Sub

        Private Sub ExecuteADOScript(ByVal SQL As String)

            'Create a new connection
            Dim connection As New SqlConnection(UpgradeConnectionString)

            'Create a new command (with no timeout)
            Dim command As New SqlCommand(SQL, connection)
            command.CommandTimeout = 0

            connection.Open()

            command.ExecuteNonQuery()

            connection.Close()

        End Sub

        Private Function GetRoleNull(ByVal RoleID As Integer) As Object
            If RoleID.ToString = Common.Globals.glbRoleNothing Then
                Return DBNull.Value
            Else
                Return RoleID
            End If
        End Function

        Private Function GetConnectionStringUserID() As String
            Dim DBUser As String = "public"
            Dim ConnSettings() As String
            Dim ConnSetting() As String
            Dim s As String

            ConnSettings = ConnectionString.Split(";"c)

            'If connection string does not use integrated security, then get user id.
            If ConnectionString.ToUpper().Contains("USER ID") Or ConnectionString.ToUpper().Contains("UID") Or ConnectionString.ToUpper().Contains("USER") Then
                ConnSettings = ConnectionString.Split(";"c)

                For Each s In ConnSettings
                    If s <> String.Empty Then
                        ConnSetting = s.Split("="c)
                        If "USER ID|UID|USER".Contains(ConnSetting(0).Trim.ToUpper()) Then
                            DBUser = ConnSetting(1).Trim()
                        End If
                    End If
                Next
            End If

            Return DBUser
        End Function

        Private Function GrantStoredProceduresPermission(ByVal Permission As String, ByVal LoginOrRole As String) As String
            Dim SQL As String = String.Empty
            Dim Exceptions As String = String.Empty
            Try
                ' grant rights to a login or role for all stored procedures
                SQL += "declare @exec nvarchar(2000) "
                SQL += "declare @name varchar(150) "
                SQL += "declare sp_cursor cursor for select o.name as name "
                SQL += "from dbo.sysobjects o "
                SQL += "where ( OBJECTPROPERTY(o.id, N'IsProcedure') = 1 or OBJECTPROPERTY(o.id, N'IsExtendedProc') = 1 or OBJECTPROPERTY(o.id, N'IsReplProc') = 1 ) "
                SQL += "and OBJECTPROPERTY(o.id, N'IsMSShipped') = 0 "
                SQL += "and o.name not like N'#%%' "
                SQL += "and (left(o.name,len('" & ObjectQualifier & "')) = '" & ObjectQualifier & "' or left(o.name,7) = 'aspnet_') "
                SQL += "open sp_cursor "
                SQL += "fetch sp_cursor into @name "
                SQL += "while @@fetch_status >= 0 "
                SQL += "begin"
                SQL += "  select @exec = 'grant " & Permission & " on ' +  @name  + ' to [" & LoginOrRole & "]'"
                SQL += "  execute (@exec)"
                SQL += "  fetch sp_cursor into @name "
                SQL += "end "
                SQL += "deallocate sp_cursor"
                SqlHelper.ExecuteNonQuery(UpgradeConnectionString, CommandType.Text, SQL)
            Catch objException As SqlException
                Exceptions += objException.ToString & vbCrLf & vbCrLf & SQL & vbCrLf & vbCrLf
            End Try
            Return Exceptions
        End Function

        Private Function GrantUserDefinedFunctionsPermission(ByVal ScalarPermission As String, ByVal TablePermission As String, ByVal LoginOrRole As String) As String
            Dim SQL As String = String.Empty
            Dim Exceptions As String = String.Empty
            Try
                ' grant EXECUTE rights to a login or role for all functions
                SQL += "declare @exec nvarchar(2000) "
                SQL += "declare @name varchar(150) "
                SQL += "declare @isscalarfunction int "
                SQL += "declare @istablefunction int "
                SQL += "declare sp_cursor cursor for select o.name as name, OBJECTPROPERTY(o.id, N'IsScalarFunction') as IsScalarFunction "
                SQL += "from dbo.sysobjects o "
                SQL += "where ( OBJECTPROPERTY(o.id, N'IsScalarFunction') = 1 OR OBJECTPROPERTY(o.id, N'IsTableFunction') = 1 ) "
                SQL += "and OBJECTPROPERTY(o.id, N'IsMSShipped') = 0 "
                SQL += "and o.name not like N'#%%' "
                SQL += "and (left(o.name,len('" & ObjectQualifier & "')) = '" & ObjectQualifier & "' or left(o.name,7) = 'aspnet_') "
                SQL += "open sp_cursor "
                SQL += "fetch sp_cursor into @name, @isscalarfunction "
                SQL += "while @@fetch_status >= 0 "
                SQL += "begin "
                SQL += "if @IsScalarFunction = 1 "
                SQL += "begin"
                SQL += "  select @exec = 'grant " & ScalarPermission & " on ' +  @name  + ' to [" & LoginOrRole & "]'"
                SQL += "  execute (@exec)"
                SQL += "  fetch sp_cursor into @name, @isscalarfunction  "
                SQL += "end "
                SQL += "else "
                SQL += "begin"
                SQL += "  select @exec = 'grant " & TablePermission & " on ' +  @name  + ' to [" & LoginOrRole & "]'"
                SQL += "  execute (@exec)"
                SQL += "  fetch sp_cursor into @name, @isscalarfunction  "
                SQL += "end "
                SQL += "end "
                SQL += "deallocate sp_cursor"
                SqlHelper.ExecuteNonQuery(UpgradeConnectionString, CommandType.Text, SQL)
            Catch objException As SqlException
                Exceptions += objException.ToString & vbCrLf & vbCrLf & SQL & vbCrLf & vbCrLf
            End Try
            Return Exceptions
        End Function

#End Region

#Region "Generic Methods"

        'Generic Methods
        '===============
        '
        ''' -----------------------------------------------------------------------------
        ''' <summary>
        ''' ExecuteReader executes a stored procedure or "dynamic sql" statement, against 
        ''' the database
        ''' </summary>
        ''' <remarks>
        ''' </remarks>
        ''' <param name="ProcedureName">The name of the Stored Procedure to Execute</param>
        ''' <param name="commandParameters">An array of parameters to pass to the Database</param>
        ''' <history>
        ''' 	[cnurse]	12/11/2005	created
        ''' </history>
        ''' -----------------------------------------------------------------------------
        Public Overrides Sub ExecuteNonQuery(ByVal ProcedureName As String, ByVal ParamArray commandParameters() As Object)
            SqlHelper.ExecuteNonQuery(ConnectionString, DatabaseOwner & ObjectQualifier & ProcedureName, commandParameters)
        End Sub

        Public Overrides Function ExecuteReader(ByVal ProcedureName As String, ByVal ParamArray commandParameters() As Object) As IDataReader
            Return SqlHelper.ExecuteReader(ConnectionString, DatabaseOwner & ObjectQualifier & ProcedureName, commandParameters)
        End Function

        Public Overrides Function ExecuteScalar(ByVal ProcedureName As String, ByVal ParamArray commandParameters() As Object) As Object
            Return SqlHelper.ExecuteScalar(ConnectionString, DatabaseOwner & ObjectQualifier & ProcedureName, commandParameters)
        End Function

        Public Overrides Function ExecuteScalar(Of T)(ByVal ProcedureName As String, ByVal ParamArray commandParameters() As Object) As T
            Return CType(ExecuteScalar(ProcedureName, commandParameters), T)
        End Function

        Public Overrides Function ExecuteDataSet(ByVal ProcedureName As String, ByVal ParamArray commandParameters() As Object) As DataSet
            Return SqlHelper.ExecuteDataset(ConnectionString, DatabaseOwner & ObjectQualifier & ProcedureName, commandParameters)
        End Function

        Public Overrides Function ExecuteSQL(ByVal SQL As String) As IDataReader
            Return ExecuteSQL(SQL, Nothing)
        End Function

        Public Overrides Function ExecuteSQL(ByVal SQL As String, ByVal ParamArray commandParameters() As IDataParameter) As IDataReader
            Dim sqlCommandParameters() As SqlParameter = Nothing
            If Not commandParameters Is Nothing Then
                sqlCommandParameters = New SqlParameter(commandParameters.Length - 1) {}
                For intIndex As Integer = 0 To commandParameters.Length - 1
                    sqlCommandParameters(intIndex) = CType(commandParameters(intIndex), SqlParameter)
                Next
            End If

            SQL = SQL.Replace("{databaseOwner}", DatabaseOwner)
            SQL = SQL.Replace("{objectQualifier}", ObjectQualifier)

            Try
                Return CType(SqlHelper.ExecuteReader(ConnectionString, CommandType.Text, SQL, sqlCommandParameters), IDataReader)
            Catch
                ' error in SQL query
                Return Nothing
            End Try
        End Function

#End Region

#Region "Abstract Method Implementation"

#Region "General Methods"

        Public Overrides Function GetConnectionStringBuilder() As DbConnectionStringBuilder
            Return New SqlConnectionStringBuilder
        End Function

        Public Overrides Function GetNull(ByVal Field As Object) As Object
            Return Null.GetNull(Field, DBNull.Value)
        End Function

#End Region

#Region "Transaction Methods"

        Public Overrides Sub CommitTransaction(ByVal transaction As DbTransaction)
            Try
                transaction.Commit()
            Finally
                If transaction IsNot Nothing AndAlso transaction.Connection IsNot Nothing Then
                    transaction.Connection.Close()
                End If
            End Try
        End Sub

        Public Overloads Overrides Function ExecuteScript(ByVal Script As String, ByVal transaction As DbTransaction) As String
            Dim SQL As String = ""
            Dim Exceptions As String = ""
            Dim Delimiter As String = "GO" & ControlChars.CrLf

            Dim arrSQL As String() = Split(Script, Delimiter, , CompareMethod.Text)

            Dim IgnoreErrors As Boolean

            For Each SQL In arrSQL
                If Trim(SQL) <> "" Then
                    ' script dynamic substitution
                    SQL = SQL.Replace("{databaseOwner}", DatabaseOwner)
                    SQL = SQL.Replace("{objectQualifier}", ObjectQualifier)

                    IgnoreErrors = False

                    If SQL.Trim.StartsWith("{IgnoreError}") Then
                        IgnoreErrors = True
                        SQL = SQL.Replace("{IgnoreError}", "")
                    End If

                    Try
                        ExecuteADOScript(CType(transaction, SqlTransaction), SQL)
                    Catch objException As SqlException
                        If Not IgnoreErrors Then
                            Exceptions += objException.ToString & vbCrLf & vbCrLf & SQL & vbCrLf & vbCrLf
                        End If
                    End Try
                End If
            Next

            Return Exceptions
        End Function

        Public Overrides Function GetTransaction() As DbTransaction
            Dim Conn As New SqlConnection(UpgradeConnectionString)
            Conn.Open()

            Dim transaction As SqlTransaction = Conn.BeginTransaction

            Return transaction

        End Function

        Public Overrides Sub RollbackTransaction(ByVal transaction As DbTransaction)
            Try
                transaction.Rollback()
            Finally
                If transaction IsNot Nothing AndAlso transaction.Connection IsNot Nothing Then
                    transaction.Connection.Close()
                End If
            End Try
        End Sub

#End Region

#Region "Install/Upgrade Methods"

        Public Overloads Overrides Function ExecuteScript(ByVal Script As String) As String
            Return ExecuteScript(Script, False)
        End Function

        Public Overloads Overrides Function ExecuteScript(ByVal Script As String, ByVal UseTransactions As Boolean) As String
            Dim SQL As String = ""
            Dim Exceptions As String = ""

            If UseTransactions Then
                Dim transaction As DbTransaction = GetTransaction()

                Try
                    Exceptions += ExecuteScript(Script, transaction)

                    If Exceptions.Length = 0 Then
                        'No exceptions so go ahead and commit
                        CommitTransaction(transaction)
                    Else
                        'Found exceptions, so rollback db
                        RollbackTransaction(transaction)
                        Exceptions += "SQL Execution failed.  Database was rolled back" & vbCrLf & vbCrLf & SQL & vbCrLf & vbCrLf
                    End If
                Finally
                    transaction.Connection.Close()
                End Try
            Else
                Dim Delimiter As String = "GO" & ControlChars.CrLf
                Dim arrSQL As String() = Split(Script, Delimiter, , CompareMethod.Text)

                For Each SQL In arrSQL
                    If Trim(SQL) <> "" Then
                        ' script dynamic substitution
                        SQL = SQL.Replace("{databaseOwner}", DatabaseOwner)
                        SQL = SQL.Replace("{objectQualifier}", ObjectQualifier)
                        Try
                            ExecuteADOScript(SQL)
                        Catch objException As SqlException
                            Exceptions += objException.ToString & vbCrLf & vbCrLf & SQL & vbCrLf & vbCrLf
                        End Try
                    End If
                Next
            End If

            ' if the upgrade connection string is specified or or db_owner setting is not set to dbo
            If UpgradeConnectionString <> ConnectionString OrElse DatabaseOwner.Trim().ToLower <> "dbo." Then
                Try
                    ' grant execute rights to the public role or userid for all stored procedures. This is
                    ' necesary because the UpgradeConnectionString will create stored procedures
                    ' which restrict execute permissions for the ConnectionString user account. This is also
                    ' necessary when db_owner is not set to "dbo" 
                    Exceptions += GrantStoredProceduresPermission("EXECUTE", GetConnectionStringUserID())
                Catch objException As SqlException
                    Exceptions += objException.ToString & vbCrLf & vbCrLf & SQL & vbCrLf & vbCrLf
                End Try
                Try
                    ' grant execute or select rights to the public role or userid for all user defined functions based
                    ' on what type of function it is (scalar function or table function). This is
                    ' necesary because the UpgradeConnectionString will create user defined functions
                    ' which restrict execute permissions for the ConnectionString user account.  This is also
                    ' necessary when db_owner is not set to "dbo" 
                    Exceptions += GrantUserDefinedFunctionsPermission("EXECUTE", "SELECT", GetConnectionStringUserID())
                Catch objException As SqlException
                    Exceptions += objException.ToString & vbCrLf & vbCrLf & SQL & vbCrLf & vbCrLf
                End Try
            End If

            Return Exceptions
        End Function

        Public Overrides Function FindDatabaseVersion(ByVal Major As Integer, ByVal Minor As Integer, ByVal Build As Integer) As IDataReader
            Return CType(SqlHelper.ExecuteReader(UpgradeConnectionString, DatabaseOwner & ObjectQualifier & "FindDatabaseVersion", Major, Minor, Build), IDataReader)
        End Function

        Public Overrides Function GetDatabaseVersion() As IDataReader
            Return CType(SqlHelper.ExecuteReader(UpgradeConnectionString, DatabaseOwner & ObjectQualifier & "GetDatabaseVersion"), IDataReader)
        End Function

        Public Overrides Function GetProviderPath() As String
            Dim objHttpContext As HttpContext = HttpContext.Current

            GetProviderPath = ProviderPath

            If GetProviderPath <> "" Then
                GetProviderPath = objHttpContext.Server.MapPath(GetProviderPath)

                If Directory.Exists(GetProviderPath) Then
                    Try
                        ' check if database is initialized
                        Dim dr As IDataReader = GetDatabaseVersion()
                        dr.Close()
                    Catch
                        ' initialize the database
                        Dim objStreamReader As StreamReader
                        objStreamReader = File.OpenText(GetProviderPath & "00.00.00." & _providerConfiguration.DefaultProvider)
                        Dim strScript As String = objStreamReader.ReadToEnd
                        objStreamReader.Close()

                        If ExecuteScript(strScript) <> "" Then
                            GetProviderPath = "ERROR: Could not connect to database specified in connectionString for SqlDataProvider"
                        End If
                    End Try
                Else
                    GetProviderPath = "ERROR: providerPath folder " & GetProviderPath & " specified for SqlDataProvider does not exist on web server"
                End If
            Else
                GetProviderPath = "ERROR: providerPath folder value not specified in web.config for SqlDataProvider"
            End If
        End Function

        Public Overrides Function TestDatabaseConnection(ByVal builder As DbConnectionStringBuilder, ByVal Owner As String, ByVal Qualifier As String) As String
            Dim sqlBuilder As SqlConnectionStringBuilder = TryCast(builder, SqlConnectionStringBuilder)
            Dim connectionString As String = Null.NullString
            If Not sqlBuilder Is Nothing Then
                connectionString = sqlBuilder.ToString()
                Dim dr As IDataReader = Nothing
                Try
                    'Check that user has permissions by attempting to create and drop a table
                    Dim objHttpContext As HttpContext = HttpContext.Current
                    ' initialize the database
                    Dim objStreamReader As StreamReader
                    objStreamReader = File.OpenText(objHttpContext.Server.MapPath(ProviderPath) & "00.00.00." & _providerConfiguration.DefaultProvider)
                    Dim strScript As String = objStreamReader.ReadToEnd
                    objStreamReader.Close()

                    Dim Delimiter As String = "GO" & ControlChars.CrLf
                    Dim arrSQL As String() = Split(strScript, Delimiter, , CompareMethod.Text)

                    For Each Sql As String In arrSQL
                        If Trim(Sql) <> "" Then
                            ' script dynamic substitution
                            Sql = Sql.Replace("{databaseOwner}", Owner)
                            Sql = Sql.Replace("{objectQualifier}", Qualifier)

                            SqlHelper.ExecuteNonQuery(connectionString, CommandType.Text, Sql)
                        End If
                    Next

                Catch ex As SqlException
                    Dim message As String = "ERROR:"
                    Dim bError As Boolean = True
                    Dim i As Integer
                    Dim errorMessages As New StringBuilder()
                    For i = 0 To ex.Errors.Count - 1
                        Dim sqlError As SqlError = ex.Errors(i)
                        If sqlError.Number = 2812 And sqlError.Class = 16 Then
                            bError = False
                            Exit For
                        Else
                            errorMessages.Append("<b>Index #:</b> " & i.ToString() & "<br/>" _
                                & "<b>Source:</b> " & sqlError.Source & "<br/>" _
                                & "<b>Class:</b> " & sqlError.Class & "<br/>" _
                                & "<b>Number:</b> " & sqlError.Number & "<br/>" _
                                & "<b>Message:</b> " & sqlError.Message & "<br/><br/>" _
                            )
                        End If
                    Next i
                    If bError Then
                        connectionString = message + errorMessages.ToString()
                    End If
                Finally
                    If Not dr Is Nothing Then
                        dr.Close()
                    End If
                End Try
            Else
                'Invalid DbConnectionStringBuilder

            End If
            Return connectionString
        End Function

        Public Overrides Sub UpgradeDatabaseSchema(ByVal Major As Integer, ByVal Minor As Integer, ByVal Build As Integer)
            ' not necessary for SQL Server - use Transact-SQL scripts
        End Sub

        Public Overrides Sub UpdateDatabaseVersion(ByVal Major As Integer, ByVal Minor As Integer, ByVal Build As Integer)
            SqlHelper.ExecuteNonQuery(UpgradeConnectionString, DatabaseOwner & ObjectQualifier & "UpdateDatabaseVersion", Major, Minor, Build)
        End Sub

#End Region

        ' host
        Public Overrides Function GetHostSettings() As IDataReader
            Return CType(SqlHelper.ExecuteReader(ConnectionString, DatabaseOwner & ObjectQualifier & "GetHostSettings"), IDataReader)
        End Function
        Public Overrides Function GetHostSetting(ByVal SettingName As String) As IDataReader
            Return CType(SqlHelper.ExecuteReader(ConnectionString, DatabaseOwner & ObjectQualifier & "GetHostSetting", SettingName), IDataReader)
        End Function
        Public Overrides Sub AddHostSetting(ByVal SettingName As String, ByVal SettingValue As String, ByVal SettingIsSecure As Boolean)
            SqlHelper.ExecuteNonQuery(ConnectionString, DatabaseOwner & ObjectQualifier & "AddHostSetting", SettingName, SettingValue, SettingIsSecure)
        End Sub
        Public Overrides Sub UpdateHostSetting(ByVal SettingName As String, ByVal SettingValue As String, ByVal SettingIsSecure As Boolean)
            SqlHelper.ExecuteNonQuery(ConnectionString, DatabaseOwner & ObjectQualifier & "UpdateHostSetting", SettingName, SettingValue, SettingIsSecure)
        End Sub

        ' portal
        Public Overrides Function AddPortalInfo(ByVal PortalName As String, ByVal Currency As String, ByVal FirstName As String, ByVal LastName As String, ByVal Username As String, ByVal Password As String, ByVal Email As String, ByVal ExpiryDate As Date, ByVal HostFee As Double, ByVal HostSpace As Double, ByVal PageQuota As Integer, ByVal UserQuota As Integer, ByVal SiteLogHistory As Integer, ByVal HomeDirectory As String) As Integer
            Return CType(SqlHelper.ExecuteScalar(ConnectionString, DatabaseOwner & ObjectQualifier & "AddPortalInfo", PortalName, Currency, GetNull(ExpiryDate), HostFee, HostSpace, PageQuota, UserQuota, GetNull(SiteLogHistory), HomeDirectory), Integer)
        End Function
        Public Overrides Function CreatePortal(ByVal PortalName As String, ByVal Currency As String, ByVal ExpiryDate As Date, ByVal HostFee As Double, ByVal HostSpace As Double, ByVal PageQuota As Integer, ByVal UserQuota As Integer, ByVal SiteLogHistory As Integer, ByVal HomeDirectory As String) As Integer
            Return CType(SqlHelper.ExecuteScalar(ConnectionString, DatabaseOwner & ObjectQualifier & "AddPortalInfo", PortalName, Currency, GetNull(ExpiryDate), HostFee, HostSpace, PageQuota, UserQuota, GetNull(SiteLogHistory), HomeDirectory), Integer)
        End Function
        Public Overrides Sub DeletePortalInfo(ByVal PortalId As Integer)
            SqlHelper.ExecuteNonQuery(ConnectionString, DatabaseOwner & ObjectQualifier & "DeletePortalInfo", PortalId)
        End Sub
        Public Overrides Function GetExpiredPortals() As IDataReader
            Return CType(SqlHelper.ExecuteReader(ConnectionString, DatabaseOwner & ObjectQualifier & "GetExpiredPortals"), IDataReader)
        End Function
        Public Overrides Function GetPortal(ByVal PortalId As Integer) As IDataReader
            Return CType(SqlHelper.ExecuteReader(ConnectionString, DatabaseOwner & ObjectQualifier & "GetPortal", PortalId), IDataReader)
        End Function
        Public Overrides Function GetPortalByAlias(ByVal PortalAlias As String) As IDataReader
            Return CType(SqlHelper.ExecuteReader(ConnectionString, DatabaseOwner & ObjectQualifier & "GetPortalByAlias", PortalAlias), IDataReader)
        End Function
        Public Overrides Function GetPortalByTab(ByVal TabId As Integer, ByVal PortalAlias As String) As IDataReader
            Return CType(SqlHelper.ExecuteReader(ConnectionString, DatabaseOwner & ObjectQualifier & "GetPortalByTab", TabId, PortalAlias), IDataReader)
        End Function
        Public Overrides Function GetPortalCount() As Integer
            Return CType(SqlHelper.ExecuteScalar(ConnectionString, DatabaseOwner & ObjectQualifier & "GetPortalCount"), Integer)
        End Function
        Public Overrides Function GetPortals() As IDataReader
            Return CType(SqlHelper.ExecuteReader(ConnectionString, DatabaseOwner & ObjectQualifier & "GetPortals"), IDataReader)
        End Function
        Public Overrides Function GetPortalsByName(ByVal nameToMatch As String, ByVal pageIndex As Integer, ByVal pageSize As Integer) As IDataReader
            Return CType(SqlHelper.ExecuteReader(ConnectionString, DatabaseOwner & ObjectQualifier & "GetPortalsByName", nameToMatch, pageIndex, pageSize), IDataReader)
        End Function
        Public Overrides Function GetPortalSpaceUsed(ByVal PortalId As Integer) As IDataReader
            Return CType(SqlHelper.ExecuteReader(ConnectionString, DatabaseOwner & ObjectQualifier & "GetPortalSpaceUsed", GetNull(PortalId)), IDataReader)
        End Function
        Public Overrides Sub UpdatePortalInfo(ByVal PortalId As Integer, ByVal PortalName As String, ByVal LogoFile As String, ByVal FooterText As String, ByVal ExpiryDate As Date, ByVal UserRegistration As Integer, ByVal BannerAdvertising As Integer, ByVal Currency As String, ByVal AdministratorId As Integer, ByVal HostFee As Double, ByVal HostSpace As Double, ByVal PageQuota As Integer, ByVal UserQuota As Integer, ByVal PaymentProcessor As String, ByVal ProcessorUserId As String, ByVal ProcessorPassword As String, ByVal Description As String, ByVal KeyWords As String, ByVal BackgroundFile As String, ByVal SiteLogHistory As Integer, ByVal SplashTabId As Integer, ByVal HomeTabId As Integer, ByVal LoginTabId As Integer, ByVal UserTabId As Integer, ByVal DefaultLanguage As String, ByVal TimeZoneOffset As Integer, ByVal HomeDirectory As String)
            SqlHelper.ExecuteNonQuery(ConnectionString, DatabaseOwner & ObjectQualifier & "UpdatePortalInfo", PortalId, PortalName, GetNull(LogoFile), GetNull(FooterText), GetNull(ExpiryDate), UserRegistration, BannerAdvertising, Currency, GetNull(AdministratorId), HostFee, HostSpace, PageQuota, UserQuota, GetNull(PaymentProcessor), GetNull(ProcessorUserId), GetNull(ProcessorPassword), GetNull(Description), GetNull(KeyWords), GetNull(BackgroundFile), GetNull(SiteLogHistory), GetNull(SplashTabId), GetNull(HomeTabId), GetNull(LoginTabId), GetNull(UserTabId), GetNull(DefaultLanguage), GetNull(TimeZoneOffset), HomeDirectory)
        End Sub
        Public Overrides Sub UpdatePortalSetup(ByVal PortalId As Integer, ByVal AdministratorId As Integer, ByVal AdministratorRoleId As Integer, ByVal RegisteredRoleId As Integer, ByVal SplashTabId As Integer, ByVal HomeTabId As Integer, ByVal LoginTabId As Integer, ByVal UserTabId As Integer, ByVal AdminTabId As Integer)
            SqlHelper.ExecuteNonQuery(ConnectionString, DatabaseOwner & ObjectQualifier & "UpdatePortalSetup", PortalId, AdministratorId, AdministratorRoleId, RegisteredRoleId, SplashTabId, HomeTabId, LoginTabId, UserTabId, AdminTabId)
        End Sub
        Public Overrides Function VerifyPortal(ByVal PortalId As Integer) As IDataReader
            Return CType(SqlHelper.ExecuteReader(ConnectionString, DatabaseOwner & ObjectQualifier & "VerifyPortal", PortalId), IDataReader)
        End Function
        Public Overrides Function VerifyPortalTab(ByVal PortalId As Integer, ByVal TabId As Integer) As IDataReader
            Return CType(SqlHelper.ExecuteReader(ConnectionString, DatabaseOwner & ObjectQualifier & "VerifyPortalTab", PortalId, TabId), IDataReader)
        End Function

        ' tab
        Public Overloads Overrides Function AddTab(ByVal PortalId As Integer, ByVal TabName As String, ByVal IsVisible As Boolean, ByVal DisableLink As Boolean, ByVal ParentId As Integer, ByVal IconFile As String, ByVal Title As String, ByVal Description As String, ByVal KeyWords As String, ByVal Url As String, ByVal SkinSrc As String, ByVal ContainerSrc As String, ByVal TabPath As String, ByVal StartDate As Date, ByVal EndDate As Date, ByVal RefreshInterval As Integer, ByVal PageHeadText As String, ByVal IsSecure As Boolean) As Integer
            Return CType(SqlHelper.ExecuteScalar(ConnectionString, DatabaseOwner & ObjectQualifier & "AddTab", GetNull(PortalId), TabName, IsVisible, DisableLink, GetNull(ParentId), IconFile, Title, Description, KeyWords, Url, GetNull(SkinSrc), GetNull(ContainerSrc), TabPath, GetNull(StartDate), GetNull(EndDate), GetNull(RefreshInterval), GetNull(PageHeadText), IsSecure), Integer)
        End Function
        <Obsolete("This method is used for legacy support during the upgrade process (pre v3.1.1). It has been replaced by one that adds the RefreshInterval and PageHeadText variables.")> _
        Public Overloads Overrides Sub UpdateTab(ByVal TabId As Integer, ByVal TabName As String, ByVal IsVisible As Boolean, ByVal DisableLink As Boolean, ByVal ParentId As Integer, ByVal IconFile As String, ByVal Title As String, ByVal Description As String, ByVal KeyWords As String, ByVal IsDeleted As Boolean, ByVal Url As String, ByVal SkinSrc As String, ByVal ContainerSrc As String, ByVal TabPath As String, ByVal StartDate As Date, ByVal EndDate As Date)
            SqlHelper.ExecuteNonQuery(ConnectionString, DatabaseOwner & ObjectQualifier & "UpdateTab", TabId, TabName, IsVisible, DisableLink, GetNull(ParentId), IconFile, Title, Description, KeyWords, IsDeleted, Url, GetNull(SkinSrc), GetNull(ContainerSrc), TabPath, GetNull(StartDate), GetNull(EndDate))
        End Sub
        Public Overloads Overrides Sub UpdateTab(ByVal TabId As Integer, ByVal TabName As String, ByVal IsVisible As Boolean, ByVal DisableLink As Boolean, ByVal ParentId As Integer, ByVal IconFile As String, ByVal Title As String, ByVal Description As String, ByVal KeyWords As String, ByVal IsDeleted As Boolean, ByVal Url As String, ByVal SkinSrc As String, ByVal ContainerSrc As String, ByVal TabPath As String, ByVal StartDate As Date, ByVal EndDate As Date, ByVal RefreshInterval As Integer, ByVal PageHeadText As String, ByVal IsSecure As Boolean)
            SqlHelper.ExecuteNonQuery(ConnectionString, DatabaseOwner & ObjectQualifier & "UpdateTab", TabId, TabName, IsVisible, DisableLink, GetNull(ParentId), IconFile, Title, Description, KeyWords, IsDeleted, Url, GetNull(SkinSrc), GetNull(ContainerSrc), TabPath, GetNull(StartDate), GetNull(EndDate), GetNull(RefreshInterval), GetNull(PageHeadText), IsSecure)
        End Sub
        Public Overrides Sub UpdateTabOrder(ByVal TabId As Integer, ByVal TabOrder As Integer, ByVal Level As Integer, ByVal ParentId As Integer, ByVal TabPath As String)
            SqlHelper.ExecuteNonQuery(ConnectionString, DatabaseOwner & ObjectQualifier & "UpdateTabOrder", TabId, TabOrder, Level, GetNull(ParentId), TabPath)
        End Sub
        Public Overrides Sub DeleteTab(ByVal TabId As Integer)
            SqlHelper.ExecuteNonQuery(ConnectionString, DatabaseOwner & ObjectQualifier & "DeleteTab", TabId)
        End Sub
        Public Overrides Function GetTabs(ByVal PortalId As Integer) As IDataReader
            Return CType(SqlHelper.ExecuteReader(ConnectionString, DatabaseOwner & ObjectQualifier & "GetTabs", GetNull(PortalId)), IDataReader)
        End Function
        Public Overrides Function GetAllTabs() As IDataReader
            Return CType(SqlHelper.ExecuteReader(ConnectionString, DatabaseOwner & ObjectQualifier & "GetAllTabs"), IDataReader)
        End Function
        Public Overrides Function GetTab(ByVal TabId As Integer) As IDataReader
            Return CType(SqlHelper.ExecuteReader(ConnectionString, DatabaseOwner & ObjectQualifier & "GetTab", TabId), IDataReader)
        End Function
        Public Overrides Function GetTabByName(ByVal TabName As String, ByVal PortalId As Integer) As IDataReader
            Return CType(SqlHelper.ExecuteReader(ConnectionString, DatabaseOwner & ObjectQualifier & "GetTabByName", TabName, GetNull(PortalId)), IDataReader)
        End Function
        Public Overrides Function GetTabCount(ByVal PortalId As Integer) As Integer
            Return CType(SqlHelper.ExecuteScalar(ConnectionString, DatabaseOwner & ObjectQualifier & "GetTabCount", PortalId), Integer)
        End Function
        Public Overrides Function GetTabsByParentId(ByVal ParentId As Integer) As IDataReader
            Return CType(SqlHelper.ExecuteReader(ConnectionString, DatabaseOwner & ObjectQualifier & "GetTabsByParentId", ParentId), IDataReader)
        End Function
        Public Overrides Function GetTabPanes(ByVal TabId As Integer) As IDataReader
            Return CType(SqlHelper.ExecuteReader(ConnectionString, DatabaseOwner & ObjectQualifier & "GetTabPanes", TabId), IDataReader)
        End Function
        Public Overrides Function GetPortalTabModules(ByVal PortalId As Integer, ByVal TabId As Integer) As IDataReader
            Return CType(SqlHelper.ExecuteReader(ConnectionString, DatabaseOwner & ObjectQualifier & "GetTabModules", TabId), IDataReader)
        End Function
        Public Overrides Function GetTabModules(ByVal TabId As Integer) As IDataReader
            Return CType(SqlHelper.ExecuteReader(ConnectionString, DatabaseOwner & ObjectQualifier & "GetTabModules", TabId), IDataReader)
        End Function

        ' module
        Public Overrides Function GetAllModules() As IDataReader
            Return CType(SqlHelper.ExecuteReader(ConnectionString, DatabaseOwner & ObjectQualifier & "GetAllModules"), IDataReader)
        End Function
        Public Overrides Function GetModules(ByVal PortalId As Integer) As IDataReader
            Return CType(SqlHelper.ExecuteReader(ConnectionString, DatabaseOwner & ObjectQualifier & "GetModules", PortalId), IDataReader)
        End Function
        Public Overrides Function GetAllTabsModules(ByVal PortalId As Integer, ByVal AllTabs As Boolean) As IDataReader
            Return CType(SqlHelper.ExecuteReader(ConnectionString, DatabaseOwner & ObjectQualifier & "GetAllTabsModules", PortalId, AllTabs), IDataReader)
        End Function
        Public Overrides Function GetModule(ByVal ModuleId As Integer, ByVal TabId As Integer) As IDataReader
            Return CType(SqlHelper.ExecuteReader(ConnectionString, DatabaseOwner & ObjectQualifier & "GetModule", ModuleId, GetNull(TabId)), IDataReader)
        End Function
        Public Overrides Function GetModuleByDefinition(ByVal PortalId As Integer, ByVal FriendlyName As String) As IDataReader
            Return CType(SqlHelper.ExecuteReader(ConnectionString, DatabaseOwner & ObjectQualifier & "GetModuleByDefinition", GetNull(PortalId), FriendlyName), IDataReader)
        End Function
        Public Overrides Function AddModule(ByVal PortalID As Integer, ByVal ModuleDefID As Integer, ByVal ModuleTitle As String, ByVal AllTabs As Boolean, ByVal Header As String, ByVal Footer As String, ByVal StartDate As DateTime, ByVal EndDate As DateTime, ByVal InheritViewPermissions As Boolean, ByVal IsDeleted As Boolean) As Integer
            Return CType(SqlHelper.ExecuteScalar(ConnectionString, DatabaseOwner & ObjectQualifier & "AddModule", GetNull(PortalID), ModuleDefID, ModuleTitle, AllTabs, GetNull(Header), GetNull(Footer), GetNull(StartDate), GetNull(EndDate), InheritViewPermissions, IsDeleted), Integer)
        End Function
        Public Overrides Sub UpdateModule(ByVal ModuleId As Integer, ByVal ModuleTitle As String, ByVal AllTabs As Boolean, ByVal Header As String, ByVal Footer As String, ByVal StartDate As DateTime, ByVal EndDate As DateTime, ByVal InheritViewPermissions As Boolean, ByVal IsDeleted As Boolean)
            SqlHelper.ExecuteNonQuery(ConnectionString, DatabaseOwner & ObjectQualifier & "UpdateModule", ModuleId, ModuleTitle, AllTabs, GetNull(Header), GetNull(Footer), GetNull(StartDate), GetNull(EndDate), InheritViewPermissions, IsDeleted)
        End Sub
        Public Overrides Sub DeleteModule(ByVal ModuleId As Integer)
            SqlHelper.ExecuteNonQuery(ConnectionString, DatabaseOwner & ObjectQualifier & "DeleteModule", ModuleId)
        End Sub

        Public Overrides Function GetTabModuleOrder(ByVal TabId As Integer, ByVal PaneName As String) As IDataReader
            Return CType(SqlHelper.ExecuteReader(ConnectionString, DatabaseOwner & ObjectQualifier & "GetTabModuleOrder", TabId, PaneName), IDataReader)
        End Function
        Public Overrides Sub UpdateModuleOrder(ByVal TabId As Integer, ByVal ModuleId As Integer, ByVal ModuleOrder As Integer, ByVal PaneName As String)
            SqlHelper.ExecuteNonQuery(ConnectionString, DatabaseOwner & ObjectQualifier & "UpdateModuleOrder", TabId, ModuleId, ModuleOrder, PaneName)
        End Sub

        Public Overrides Sub AddTabModule(ByVal TabId As Integer, ByVal ModuleId As Integer, ByVal ModuleOrder As Integer, ByVal PaneName As String, ByVal CacheTime As Integer, ByVal Alignment As String, ByVal Color As String, ByVal Border As String, ByVal IconFile As String, ByVal Visibility As Integer, ByVal ContainerSrc As String, ByVal DisplayTitle As Boolean, ByVal DisplayPrint As Boolean, ByVal DisplaySyndicate As Boolean)
            SqlHelper.ExecuteNonQuery(ConnectionString, DatabaseOwner & ObjectQualifier & "AddTabModule", TabId, ModuleId, ModuleOrder, PaneName, CacheTime, GetNull(Alignment), GetNull(Color), GetNull(Border), GetNull(IconFile), Visibility, GetNull(ContainerSrc), DisplayTitle, DisplayPrint, DisplaySyndicate)
        End Sub
        Public Overrides Sub UpdateTabModule(ByVal TabId As Integer, ByVal ModuleId As Integer, ByVal ModuleOrder As Integer, ByVal PaneName As String, ByVal CacheTime As Integer, ByVal Alignment As String, ByVal Color As String, ByVal Border As String, ByVal IconFile As String, ByVal Visibility As Integer, ByVal ContainerSrc As String, ByVal DisplayTitle As Boolean, ByVal DisplayPrint As Boolean, ByVal DisplaySyndicate As Boolean)
            SqlHelper.ExecuteNonQuery(ConnectionString, DatabaseOwner & ObjectQualifier & "UpdateTabModule", TabId, ModuleId, ModuleOrder, PaneName, CacheTime, GetNull(Alignment), GetNull(Color), GetNull(Border), GetNull(IconFile), Visibility, GetNull(ContainerSrc), DisplayTitle, DisplayPrint, DisplaySyndicate)
        End Sub
        Public Overrides Sub DeleteTabModule(ByVal TabId As Integer, ByVal ModuleId As Integer)
            SqlHelper.ExecuteNonQuery(ConnectionString, DatabaseOwner & ObjectQualifier & "DeleteTabModule", TabId, ModuleId)
        End Sub
        Public Overrides Function GetSearchModules(ByVal PortalId As Integer) As IDataReader
            Return CType(SqlHelper.ExecuteReader(ConnectionString, DatabaseOwner & ObjectQualifier & "GetSearchModules", PortalId), IDataReader)
        End Function
        Public Overrides Function GetModuleSettings(ByVal ModuleId As Integer) As IDataReader
            Return CType(SqlHelper.ExecuteReader(ConnectionString, DatabaseOwner & ObjectQualifier & "GetModuleSettings", ModuleId), IDataReader)
        End Function
        Public Overrides Function GetModuleSetting(ByVal ModuleId As Integer, ByVal SettingName As String) As IDataReader
            Return CType(SqlHelper.ExecuteReader(ConnectionString, DatabaseOwner & ObjectQualifier & "GetModuleSetting", ModuleId, SettingName), IDataReader)
        End Function
        Public Overrides Sub AddModuleSetting(ByVal ModuleId As Integer, ByVal SettingName As String, ByVal SettingValue As String)
            SqlHelper.ExecuteNonQuery(ConnectionString, DatabaseOwner & ObjectQualifier & "AddModuleSetting", ModuleId, SettingName, SettingValue)
        End Sub
        Public Overrides Sub UpdateModuleSetting(ByVal ModuleId As Integer, ByVal SettingName As String, ByVal SettingValue As String)
            SqlHelper.ExecuteNonQuery(ConnectionString, DatabaseOwner & ObjectQualifier & "UpdateModuleSetting", ModuleId, SettingName, SettingValue)
        End Sub
        Public Overrides Sub DeleteModuleSetting(ByVal ModuleId As Integer, ByVal SettingName As String)
            SqlHelper.ExecuteNonQuery(ConnectionString, DatabaseOwner & ObjectQualifier & "DeleteModuleSetting", ModuleId, SettingName)
        End Sub
        Public Overrides Sub DeleteModuleSettings(ByVal ModuleId As Integer)
            SqlHelper.ExecuteNonQuery(ConnectionString, DatabaseOwner & ObjectQualifier & "DeleteModuleSettings", ModuleId)
        End Sub

        Public Overrides Function GetTabModuleSettings(ByVal TabModuleId As Integer) As IDataReader
            Return CType(SqlHelper.ExecuteReader(ConnectionString, DatabaseOwner & ObjectQualifier & "GetTabModuleSettings", TabModuleId), IDataReader)
        End Function
        Public Overrides Function GetTabModuleSetting(ByVal TabModuleId As Integer, ByVal SettingName As String) As IDataReader
            Return CType(SqlHelper.ExecuteReader(ConnectionString, DatabaseOwner & ObjectQualifier & "GetTabModuleSetting", TabModuleId, SettingName), IDataReader)
        End Function
        Public Overrides Sub AddTabModuleSetting(ByVal TabModuleId As Integer, ByVal SettingName As String, ByVal SettingValue As String)
            SqlHelper.ExecuteNonQuery(ConnectionString, DatabaseOwner & ObjectQualifier & "AddTabModuleSetting", TabModuleId, SettingName, SettingValue)
        End Sub
        Public Overrides Sub UpdateTabModuleSetting(ByVal TabModuleId As Integer, ByVal SettingName As String, ByVal SettingValue As String)
            SqlHelper.ExecuteNonQuery(ConnectionString, DatabaseOwner & ObjectQualifier & "UpdateTabModuleSetting", TabModuleId, SettingName, SettingValue)
        End Sub
        Public Overrides Sub DeleteTabModuleSetting(ByVal TabModuleId As Integer, ByVal SettingName As String)
            SqlHelper.ExecuteNonQuery(ConnectionString, DatabaseOwner & ObjectQualifier & "DeleteTabModuleSetting", TabModuleId, SettingName)
        End Sub
        Public Overrides Sub DeleteTabModuleSettings(ByVal TabModuleId As Integer)
            SqlHelper.ExecuteNonQuery(ConnectionString, DatabaseOwner & ObjectQualifier & "DeleteTabModuleSettings", TabModuleId)
        End Sub

        ' module definition
        Public Overrides Function GetDesktopModule(ByVal DesktopModuleId As Integer) As IDataReader
            Return CType(SqlHelper.ExecuteReader(ConnectionString, DatabaseOwner & ObjectQualifier & "GetDesktopModule", DesktopModuleId), IDataReader)
        End Function
        Public Overrides Function GetDesktopModuleByFriendlyName(ByVal FriendlyName As String) As IDataReader
            Return CType(SqlHelper.ExecuteReader(ConnectionString, DatabaseOwner & ObjectQualifier & "GetDesktopModuleByFriendlyName", FriendlyName), IDataReader)
        End Function
        Public Overrides Function GetDesktopModuleByModuleName(ByVal ModuleName As String) As IDataReader
            Return CType(SqlHelper.ExecuteReader(ConnectionString, DatabaseOwner & ObjectQualifier & "GetDesktopModuleByModuleName", ModuleName), IDataReader)
        End Function
        Public Overrides Function GetDesktopModules() As IDataReader
            Return CType(SqlHelper.ExecuteReader(ConnectionString, DatabaseOwner & ObjectQualifier & "GetDesktopModules"), IDataReader)
        End Function
        Public Overrides Function GetDesktopModulesByPortal(ByVal PortalId As Integer) As IDataReader
            Return CType(SqlHelper.ExecuteReader(ConnectionString, DatabaseOwner & ObjectQualifier & "GetDesktopModulesByPortal", PortalId), IDataReader)
        End Function
        Public Overrides Function AddDesktopModule(ByVal ModuleName As String, ByVal FolderName As String, ByVal FriendlyName As String, ByVal Description As String, ByVal Version As String, ByVal IsPremium As Boolean, ByVal IsAdmin As Boolean, ByVal BusinessControllerClass As String, ByVal SupportedFeatures As Integer, ByVal CompatibleVersions As String, ByVal Dependencies As String, ByVal Permissions As String) As Integer
            Return CType(SqlHelper.ExecuteScalar(ConnectionString, DatabaseOwner & ObjectQualifier & "AddDesktopModule", ModuleName, FolderName, FriendlyName, GetNull(Description), GetNull(Version), IsPremium, IsAdmin, BusinessControllerClass, SupportedFeatures, GetNull(CompatibleVersions), GetNull(Dependencies), GetNull(Permissions)), Integer)
        End Function
        Public Overrides Sub UpdateDesktopModule(ByVal DesktopModuleId As Integer, ByVal ModuleName As String, ByVal FolderName As String, ByVal FriendlyName As String, ByVal Description As String, ByVal Version As String, ByVal IsPremium As Boolean, ByVal IsAdmin As Boolean, ByVal BusinessControllerClass As String, ByVal SupportedFeatures As Integer, ByVal CompatibleVersions As String, ByVal Dependencies As String, ByVal Permissions As String)
            SqlHelper.ExecuteNonQuery(ConnectionString, DatabaseOwner & ObjectQualifier & "UpdateDesktopModule", DesktopModuleId, ModuleName, FolderName, FriendlyName, GetNull(Description), GetNull(Version), IsPremium, IsAdmin, BusinessControllerClass, SupportedFeatures, GetNull(CompatibleVersions), GetNull(Dependencies), GetNull(Permissions))
        End Sub
        Public Overrides Sub DeleteDesktopModule(ByVal DesktopModuleId As Integer)
            SqlHelper.ExecuteNonQuery(ConnectionString, DatabaseOwner & ObjectQualifier & "DeleteDesktopModule", DesktopModuleId)
        End Sub

        Public Overrides Function GetPortalDesktopModules(ByVal PortalId As Integer, ByVal DesktopModuleId As Integer) As IDataReader
            Return CType(SqlHelper.ExecuteReader(ConnectionString, DatabaseOwner & ObjectQualifier & "GetPortalDesktopModules", GetNull(PortalId), GetNull(DesktopModuleId)), IDataReader)
        End Function
        Public Overrides Function AddPortalDesktopModule(ByVal PortalId As Integer, ByVal DesktopModuleId As Integer) As Integer
            Return CType(SqlHelper.ExecuteScalar(ConnectionString, DatabaseOwner & ObjectQualifier & "AddPortalDesktopModule", PortalId, DesktopModuleId), Integer)
        End Function
        Public Overrides Sub DeletePortalDesktopModules(ByVal PortalId As Integer, ByVal DesktopModuleId As Integer)
            SqlHelper.ExecuteNonQuery(ConnectionString, DatabaseOwner & ObjectQualifier & "DeletePortalDesktopModules", GetNull(PortalId), GetNull(DesktopModuleId))
        End Sub

        Public Overrides Function GetModuleDefinitions(ByVal DesktopModuleId As Integer) As IDataReader
            Return CType(SqlHelper.ExecuteReader(ConnectionString, DatabaseOwner & ObjectQualifier & "GetModuleDefinitions", DesktopModuleId), IDataReader)
        End Function
        Public Overrides Function GetModuleDefinition(ByVal ModuleDefId As Integer) As IDataReader
            Return CType(SqlHelper.ExecuteReader(ConnectionString, DatabaseOwner & ObjectQualifier & "GetModuleDefinition", ModuleDefId), IDataReader)
        End Function
        Public Overrides Function GetModuleDefinitionByName(ByVal DesktopModuleId As Integer, ByVal FriendlyName As String) As IDataReader
            Return CType(SqlHelper.ExecuteReader(ConnectionString, DatabaseOwner & ObjectQualifier & "GetModuleDefinitionByName", DesktopModuleId, FriendlyName), IDataReader)
        End Function
        Public Overrides Function AddModuleDefinition(ByVal DesktopModuleId As Integer, ByVal FriendlyName As String, ByVal DefaultCacheTime As Integer) As Integer
            Return CType(SqlHelper.ExecuteScalar(ConnectionString, DatabaseOwner & ObjectQualifier & "AddModuleDefinition", DesktopModuleId, FriendlyName, DefaultCacheTime), Integer)
        End Function
        Public Overrides Sub DeleteModuleDefinition(ByVal ModuleDefId As Integer)
            SqlHelper.ExecuteNonQuery(ConnectionString, DatabaseOwner & ObjectQualifier & "DeleteModuleDefinition", ModuleDefId)
        End Sub
        Public Overrides Sub UpdateModuleDefinition(ByVal ModuleDefId As Integer, ByVal FriendlyName As String, ByVal DefaultCacheTime As Integer)
            SqlHelper.ExecuteNonQuery(ConnectionString, DatabaseOwner & ObjectQualifier & "UpdateModuleDefinition", ModuleDefId, FriendlyName, DefaultCacheTime)
        End Sub

        Public Overrides Function GetModuleControl(ByVal ModuleControlId As Integer) As IDataReader
            Return CType(SqlHelper.ExecuteReader(ConnectionString, DatabaseOwner & ObjectQualifier & "GetModuleControl", ModuleControlId), IDataReader)
        End Function
        Public Overrides Function GetModuleControls(ByVal ModuleDefId As Integer) As IDataReader
            Return CType(SqlHelper.ExecuteReader(ConnectionString, DatabaseOwner & ObjectQualifier & "GetModuleControls", GetNull(ModuleDefId)), IDataReader)
        End Function
        Public Overrides Function GetModuleControlsByKey(ByVal ControlKey As String, ByVal ModuleDefId As Integer) As IDataReader
            Return CType(SqlHelper.ExecuteReader(ConnectionString, DatabaseOwner & ObjectQualifier & "GetModuleControlsByKey", GetNull(ControlKey), GetNull(ModuleDefId)), IDataReader)
        End Function
        Public Overrides Function GetModuleControlByKeyAndSrc(ByVal ModuleDefID As Integer, ByVal ControlKey As String, ByVal ControlSrc As String) As IDataReader
            Return CType(SqlHelper.ExecuteReader(ConnectionString, DatabaseOwner & ObjectQualifier & "GetModuleControlByKeyAndSrc", GetNull(ModuleDefID), GetNull(ControlKey), GetNull(ControlSrc)), IDataReader)
        End Function
        Public Overrides Function AddModuleControl(ByVal ModuleDefId As Integer, ByVal ControlKey As String, ByVal ControlTitle As String, ByVal ControlSrc As String, ByVal IconFile As String, ByVal ControlType As Integer, ByVal ViewOrder As Integer, ByVal HelpUrl As String, ByVal SupportsPartialRendering As Boolean) As Integer
            Return CType(SqlHelper.ExecuteScalar(ConnectionString, DatabaseOwner & ObjectQualifier & "AddModuleControl", GetNull(ModuleDefId), GetNull(ControlKey), GetNull(ControlTitle), ControlSrc, GetNull(IconFile), ControlType, GetNull(ViewOrder), GetNull(HelpUrl), SupportsPartialRendering), Integer)
        End Function
        Public Overrides Sub UpdateModuleControl(ByVal ModuleControlId As Integer, ByVal ModuleDefId As Integer, ByVal ControlKey As String, ByVal ControlTitle As String, ByVal ControlSrc As String, ByVal IconFile As String, ByVal ControlType As Integer, ByVal ViewOrder As Integer, ByVal HelpUrl As String, ByVal SupportsPartialRendering As Boolean)
            SqlHelper.ExecuteNonQuery(ConnectionString, DatabaseOwner & ObjectQualifier & "UpdateModuleControl", ModuleControlId, GetNull(ModuleDefId), GetNull(ControlKey), GetNull(ControlTitle), ControlSrc, GetNull(IconFile), ControlType, GetNull(ViewOrder), GetNull(HelpUrl), SupportsPartialRendering)
        End Sub
        Public Overrides Sub DeleteModuleControl(ByVal ModuleControlId As Integer)
            SqlHelper.ExecuteNonQuery(ConnectionString, DatabaseOwner & ObjectQualifier & "DeleteModuleControl", ModuleControlId)
        End Sub

        ' files
        Public Overrides Function GetFiles(ByVal PortalId As Integer, ByVal FolderID As Integer) As IDataReader
            Return CType(SqlHelper.ExecuteReader(ConnectionString, DatabaseOwner & ObjectQualifier & "GetFiles", GetNull(PortalId), FolderID), IDataReader)
        End Function
        Public Overrides Function GetFile(ByVal FileName As String, ByVal PortalId As Integer, ByVal FolderID As Integer) As IDataReader
            Return CType(SqlHelper.ExecuteReader(ConnectionString, DatabaseOwner & ObjectQualifier & "GetFile", FileName, GetNull(PortalId), FolderID), IDataReader)
        End Function
        Public Overrides Function GetFileById(ByVal FileId As Integer, ByVal PortalId As Integer) As IDataReader
            Return CType(SqlHelper.ExecuteReader(ConnectionString, DatabaseOwner & ObjectQualifier & "GetFileById", FileId, GetNull(PortalId)), IDataReader)
        End Function
        Public Overrides Sub DeleteFile(ByVal PortalId As Integer, ByVal FileName As String, ByVal FolderID As Integer)
            SqlHelper.ExecuteNonQuery(ConnectionString, DatabaseOwner & ObjectQualifier & "DeleteFile", GetNull(PortalId), FileName, FolderID)
        End Sub
        Public Overrides Sub DeleteFiles(ByVal PortalId As Integer)
            SqlHelper.ExecuteNonQuery(ConnectionString, DatabaseOwner & ObjectQualifier & "DeleteFiles", GetNull(PortalId))
        End Sub
        Public Overrides Function AddFile(ByVal PortalId As Integer, ByVal FileName As String, ByVal Extension As String, ByVal Size As Long, ByVal Width As Integer, ByVal Height As Integer, ByVal ContentType As String, ByVal Folder As String, ByVal FolderID As Integer) As Integer
            Return CType(SqlHelper.ExecuteScalar(ConnectionString, DatabaseOwner & ObjectQualifier & "AddFile", GetNull(PortalId), FileName, Extension, Size, GetNull(Width), GetNull(Height), ContentType, Folder, FolderID), Integer)
        End Function
        Public Overrides Sub UpdateFile(ByVal FileId As Integer, ByVal FileName As String, ByVal Extension As String, ByVal Size As Long, ByVal Width As Integer, ByVal Height As Integer, ByVal ContentType As String, ByVal Folder As String, ByVal FolderID As Integer)
            SqlHelper.ExecuteNonQuery(ConnectionString, DatabaseOwner & ObjectQualifier & "UpdateFile", FileId, FileName, Extension, Size, GetNull(Width), GetNull(Height), ContentType, Folder, FolderID)
        End Sub
        Public Overrides Function GetAllFiles() As DataTable
            Return SqlHelper.ExecuteDataset(ConnectionString, DatabaseOwner & ObjectQualifier & "GetAllFiles").Tables(0)
        End Function
        Public Overrides Function GetFileContent(ByVal FileId As Integer, ByVal PortalId As Integer) As IDataReader
            Return CType(SqlHelper.ExecuteReader(ConnectionString, DatabaseOwner & ObjectQualifier & "GetFileContent", FileId, GetNull(PortalId)), IDataReader)
        End Function
        Public Overrides Sub UpdateFileContent(ByVal FileId As Integer, ByVal Content() As Byte)
            SqlHelper.ExecuteNonQuery(ConnectionString, DatabaseOwner & ObjectQualifier & "UpdateFileContent", FileId, GetNull(Content))
        End Sub

        ' site log
        Public Overrides Sub AddSiteLog(ByVal DateTime As Date, ByVal PortalId As Integer, ByVal UserId As Integer, ByVal Referrer As String, ByVal URL As String, ByVal UserAgent As String, ByVal UserHostAddress As String, ByVal UserHostName As String, ByVal TabId As Integer, ByVal AffiliateId As Integer)
            SqlHelper.ExecuteNonQuery(ConnectionString, DatabaseOwner & ObjectQualifier & "AddSiteLog", DateTime, PortalId, GetNull(UserId), GetNull(Referrer), GetNull(URL), GetNull(UserAgent), GetNull(UserHostAddress), GetNull(UserHostName), GetNull(TabId), GetNull(AffiliateId))
        End Sub
        Public Overrides Function GetSiteLog(ByVal PortalId As Integer, ByVal PortalAlias As String, ByVal ReportName As String, ByVal StartDate As Date, ByVal EndDate As Date) As IDataReader
            Return CType(SqlHelper.ExecuteReader(ConnectionString, DatabaseOwner & ObjectQualifier & ReportName, PortalId, PortalAlias, StartDate, EndDate), IDataReader)
        End Function
        Public Overrides Function GetSiteLogReports() As IDataReader
            Return CType(SqlHelper.ExecuteReader(ConnectionString, DatabaseOwner & ObjectQualifier & "GetSiteLogReports"), IDataReader)
        End Function
        Public Overrides Sub DeleteSiteLog(ByVal DateTime As Date, ByVal PortalId As Integer)
            SqlHelper.ExecuteNonQuery(ConnectionString, DatabaseOwner & ObjectQualifier & "DeleteSiteLog", DateTime, PortalId)
        End Sub

        ' database
        Public Overrides Function GetTables() As IDataReader
            Return CType(SqlHelper.ExecuteReader(ConnectionString, DatabaseOwner & ObjectQualifier & "GetTables"), IDataReader)
        End Function
        Public Overrides Function GetFields(ByVal TableName As String) As IDataReader
            Dim SQL As String = "SELECT * FROM {objectQualifier}" & TableName & " WHERE 1 = 0"
            Return ExecuteSQL(SQL)
        End Function

        ' vendors
        Public Overrides Function GetVendors(ByVal PortalId As Integer, ByVal UnAuthorized As Boolean, ByVal PageIndex As Integer, ByVal PageSize As Integer) As IDataReader
            Return CType(SqlHelper.ExecuteReader(ConnectionString, DatabaseOwner & ObjectQualifier & "GetVendors", GetNull(PortalId), UnAuthorized, GetNull(PageSize), GetNull(PageIndex)), IDataReader)
        End Function
        Public Overrides Function GetVendorsByEmail(ByVal Filter As String, ByVal PortalId As Integer, ByVal PageIndex As Integer, ByVal PageSize As Integer) As IDataReader
            Return CType(SqlHelper.ExecuteReader(ConnectionString, DatabaseOwner & ObjectQualifier & "GetVendorsByEmail", Filter, GetNull(PortalId), GetNull(PageSize), GetNull(PageIndex)), IDataReader)
        End Function
        Public Overrides Function GetVendorsByName(ByVal Filter As String, ByVal PortalId As Integer, ByVal PageIndex As Integer, ByVal PageSize As Integer) As IDataReader
            Return CType(SqlHelper.ExecuteReader(ConnectionString, DatabaseOwner & ObjectQualifier & "GetVendorsByName", Filter, GetNull(PortalId), GetNull(PageSize), GetNull(PageIndex)), IDataReader)
        End Function
        Public Overrides Function GetVendor(ByVal VendorId As Integer, ByVal PortalId As Integer) As IDataReader
            Return CType(SqlHelper.ExecuteReader(ConnectionString, DatabaseOwner & ObjectQualifier & "GetVendor", VendorId, GetNull(PortalId)), IDataReader)
        End Function
        Public Overrides Sub DeleteVendor(ByVal VendorId As Integer)
            SqlHelper.ExecuteNonQuery(ConnectionString, DatabaseOwner & ObjectQualifier & "DeleteVendor", VendorId)
        End Sub
        Public Overrides Function AddVendor(ByVal PortalId As Integer, ByVal VendorName As String, ByVal Unit As String, ByVal Street As String, ByVal City As String, ByVal Region As String, ByVal Country As String, ByVal PostalCode As String, ByVal Telephone As String, ByVal Fax As String, ByVal Cell As String, ByVal Email As String, ByVal Website As String, ByVal FirstName As String, ByVal LastName As String, ByVal UserName As String, ByVal LogoFile As String, ByVal KeyWords As String, ByVal Authorized As String) As Integer
            Return CType(SqlHelper.ExecuteScalar(ConnectionString, DatabaseOwner & ObjectQualifier & "AddVendor", GetNull(PortalId), VendorName, Unit, Street, City, Region, Country, PostalCode, Telephone, Fax, Cell, Email, Website, FirstName, LastName, UserName, LogoFile, KeyWords, Boolean.Parse(Authorized)), Integer)
        End Function
        Public Overrides Sub UpdateVendor(ByVal VendorId As Integer, ByVal VendorName As String, ByVal Unit As String, ByVal Street As String, ByVal City As String, ByVal Region As String, ByVal Country As String, ByVal PostalCode As String, ByVal Telephone As String, ByVal Fax As String, ByVal Cell As String, ByVal Email As String, ByVal Website As String, ByVal FirstName As String, ByVal LastName As String, ByVal UserName As String, ByVal LogoFile As String, ByVal KeyWords As String, ByVal Authorized As String)
            SqlHelper.ExecuteNonQuery(ConnectionString, DatabaseOwner & ObjectQualifier & "UpdateVendor", VendorId, VendorName, Unit, Street, City, Region, Country, PostalCode, Telephone, Fax, Cell, Email, Website, FirstName, LastName, UserName, LogoFile, KeyWords, Boolean.Parse(Authorized))
        End Sub
        Public Overrides Function GetVendorClassifications(ByVal VendorId As Integer) As IDataReader
            Return CType(SqlHelper.ExecuteReader(ConnectionString, DatabaseOwner & ObjectQualifier & "GetVendorClassifications", GetNull(VendorId)), IDataReader)
        End Function
        Public Overrides Sub DeleteVendorClassifications(ByVal VendorId As Integer)
            SqlHelper.ExecuteNonQuery(ConnectionString, DatabaseOwner & ObjectQualifier & "DeleteVendorClassifications", VendorId)
        End Sub
        Public Overrides Function AddVendorClassification(ByVal VendorId As Integer, ByVal ClassificationId As Integer) As Integer
            Return CType(SqlHelper.ExecuteScalar(ConnectionString, DatabaseOwner & ObjectQualifier & "AddVendorClassification", VendorId, ClassificationId), Integer)
        End Function

        ' banners
        Public Overrides Function GetBanners(ByVal VendorId As Integer) As IDataReader
            Return CType(SqlHelper.ExecuteReader(ConnectionString, DatabaseOwner & ObjectQualifier & "GetBanners", VendorId), IDataReader)
        End Function
        Public Overrides Function GetBanner(ByVal BannerId As Integer, ByVal VendorId As Integer, ByVal PortalId As Integer) As IDataReader
            Return CType(SqlHelper.ExecuteReader(ConnectionString, DatabaseOwner & ObjectQualifier & "GetBanner", BannerId, VendorId, GetNull(PortalId)), IDataReader)
        End Function
        Public Overrides Function GetBannerGroups(ByVal PortalId As Integer) As DataTable
            Return SqlHelper.ExecuteDataset(ConnectionString, DatabaseOwner & ObjectQualifier & "GetBannerGroups", GetNull(PortalId)).Tables(0)
        End Function
        Public Overrides Sub DeleteBanner(ByVal BannerId As Integer)
            SqlHelper.ExecuteNonQuery(ConnectionString, DatabaseOwner & ObjectQualifier & "DeleteBanner", BannerId)
        End Sub
        Public Overrides Function AddBanner(ByVal BannerName As String, ByVal VendorId As Integer, ByVal ImageFile As String, ByVal URL As String, ByVal Impressions As Integer, ByVal CPM As Double, ByVal StartDate As Date, ByVal EndDate As Date, ByVal UserName As String, ByVal BannerTypeId As Integer, ByVal Description As String, ByVal GroupName As String, ByVal Criteria As Integer, ByVal Width As Integer, ByVal Height As Integer) As Integer
            Return CType(SqlHelper.ExecuteScalar(ConnectionString, DatabaseOwner & ObjectQualifier & "AddBanner", BannerName, VendorId, GetNull(ImageFile), GetNull(URL), Impressions, CPM, GetNull(StartDate), GetNull(EndDate), UserName, BannerTypeId, GetNull(Description), GetNull(GroupName), Criteria, Width, Height), Integer)
        End Function
        Public Overrides Sub UpdateBanner(ByVal BannerId As Integer, ByVal BannerName As String, ByVal ImageFile As String, ByVal URL As String, ByVal Impressions As Integer, ByVal CPM As Double, ByVal StartDate As Date, ByVal EndDate As Date, ByVal UserName As String, ByVal BannerTypeId As Integer, ByVal Description As String, ByVal GroupName As String, ByVal Criteria As Integer, ByVal Width As Integer, ByVal Height As Integer)
            SqlHelper.ExecuteNonQuery(ConnectionString, DatabaseOwner & ObjectQualifier & "UpdateBanner", BannerId, BannerName, GetNull(ImageFile), GetNull(URL), Impressions, CPM, GetNull(StartDate), GetNull(EndDate), UserName, BannerTypeId, GetNull(Description), GetNull(GroupName), Criteria, Width, Height)
        End Sub
        Public Overrides Function FindBanners(ByVal PortalId As Integer, ByVal BannerTypeId As Integer, ByVal GroupName As String) As IDataReader
            Return CType(SqlHelper.ExecuteReader(ConnectionString, DatabaseOwner & ObjectQualifier & "FindBanners", GetNull(PortalId), GetNull(BannerTypeId), GetNull(GroupName)), IDataReader)
        End Function
        Public Overrides Sub UpdateBannerViews(ByVal BannerId As Integer, ByVal StartDate As Date, ByVal EndDate As Date)
            SqlHelper.ExecuteNonQuery(ConnectionString, DatabaseOwner & ObjectQualifier & "UpdateBannerViews", BannerId, GetNull(StartDate), GetNull(EndDate))
        End Sub
        Public Overrides Sub UpdateBannerClickThrough(ByVal BannerId As Integer, ByVal VendorId As Integer)
            SqlHelper.ExecuteNonQuery(ConnectionString, DatabaseOwner & ObjectQualifier & "UpdateBannerClickThrough", BannerId, VendorId)
        End Sub

        ' affiliates
        Public Overrides Function GetAffiliates(ByVal VendorId As Integer) As IDataReader
            Return CType(SqlHelper.ExecuteReader(ConnectionString, DatabaseOwner & ObjectQualifier & "GetAffiliates", VendorId), IDataReader)
        End Function
        Public Overrides Function GetAffiliate(ByVal AffiliateId As Integer, ByVal VendorId As Integer, ByVal PortalId As Integer) As IDataReader
            Return CType(SqlHelper.ExecuteReader(ConnectionString, DatabaseOwner & ObjectQualifier & "GetAffiliate", AffiliateId, VendorId, GetNull(PortalId)), IDataReader)
        End Function
        Public Overrides Sub DeleteAffiliate(ByVal AffiliateId As Integer)
            SqlHelper.ExecuteNonQuery(ConnectionString, DatabaseOwner & ObjectQualifier & "DeleteAffiliate", AffiliateId)
        End Sub
        Public Overrides Function AddAffiliate(ByVal VendorId As Integer, ByVal StartDate As Date, ByVal EndDate As Date, ByVal CPC As Double, ByVal CPA As Double) As Integer
            Return CType(SqlHelper.ExecuteScalar(ConnectionString, DatabaseOwner & ObjectQualifier & "AddAffiliate", VendorId, GetNull(StartDate), GetNull(EndDate), CPC, CPA), Integer)
        End Function
        Public Overrides Sub UpdateAffiliate(ByVal AffiliateId As Integer, ByVal StartDate As Date, ByVal EndDate As Date, ByVal CPC As Double, ByVal CPA As Double)
            SqlHelper.ExecuteNonQuery(ConnectionString, DatabaseOwner & ObjectQualifier & "UpdateAffiliate", AffiliateId, GetNull(StartDate), GetNull(EndDate), CPC, CPA)
        End Sub
        Public Overrides Sub UpdateAffiliateStats(ByVal AffiliateId As Integer, ByVal Clicks As Integer, ByVal Acquisitions As Integer)
            SqlHelper.ExecuteNonQuery(ConnectionString, DatabaseOwner & ObjectQualifier & "UpdateAffiliateStats", AffiliateId, Clicks, Acquisitions)
        End Sub

        ' skins/containers
        Public Overrides Function GetSkin(ByVal SkinRoot As String, ByVal PortalId As Integer, ByVal SkinType As Integer) As IDataReader
            Return CType(SqlHelper.ExecuteReader(ConnectionString, DatabaseOwner & ObjectQualifier & "GetSkin", SkinRoot, GetNull(PortalId), SkinType), IDataReader)
        End Function
        Public Overrides Function GetSkins(ByVal PortalId As Integer) As IDataReader
            Return CType(SqlHelper.ExecuteReader(ConnectionString, DatabaseOwner & ObjectQualifier & "GetSkins", GetNull(PortalId)), IDataReader)
        End Function
        Public Overrides Sub DeleteSkin(ByVal SkinRoot As String, ByVal PortalId As Integer, ByVal SkinType As Integer)
            SqlHelper.ExecuteNonQuery(ConnectionString, DatabaseOwner & ObjectQualifier & "DeleteSkin", SkinRoot, GetNull(PortalId), SkinType)
        End Sub
        Public Overrides Function AddSkin(ByVal SkinRoot As String, ByVal PortalId As Integer, ByVal SkinType As Integer, ByVal SkinSrc As String) As Integer
            Return CType(SqlHelper.ExecuteScalar(ConnectionString, DatabaseOwner & ObjectQualifier & "AddSkin", SkinRoot, GetNull(PortalId), SkinType, SkinSrc), Integer)
        End Function
        Public Overrides Function CanDeleteSkin(ByVal SkinType As String, ByVal SkinFoldername As String) As Boolean
            Return CType(SqlHelper.ExecuteScalar(ConnectionString, DatabaseOwner & ObjectQualifier & "CanDeleteSkin", SkinType, SkinFoldername), Boolean)
        End Function

        ' personalization
        Public Overrides Function GetAllProfiles() As IDataReader
            Return CType(SqlHelper.ExecuteReader(ConnectionString, DatabaseOwner & ObjectQualifier & "GetAllProfiles"), IDataReader)
        End Function
        Public Overrides Function GetProfile(ByVal UserId As Integer, ByVal PortalId As Integer) As IDataReader
            Return CType(SqlHelper.ExecuteReader(ConnectionString, DatabaseOwner & ObjectQualifier & "GetProfile", UserId, PortalId), IDataReader)
        End Function
        Public Overrides Sub AddProfile(ByVal UserId As Integer, ByVal PortalId As Integer)
            SqlHelper.ExecuteNonQuery(ConnectionString, DatabaseOwner & ObjectQualifier & "AddProfile", UserId, PortalId)
        End Sub
        Public Overrides Sub UpdateProfile(ByVal UserId As Integer, ByVal PortalId As Integer, ByVal ProfileData As String)
            SqlHelper.ExecuteNonQuery(ConnectionString, DatabaseOwner & ObjectQualifier & "UpdateProfile", UserId, PortalId, ProfileData)
        End Sub

        'profile property definitions
        Public Overrides Function AddPropertyDefinition(ByVal PortalId As Integer, ByVal ModuleDefId As Integer, ByVal DataType As Integer, ByVal DefaultValue As String, ByVal PropertyCategory As String, ByVal PropertyName As String, ByVal Required As Boolean, ByVal ValidationExpression As String, ByVal ViewOrder As Integer, ByVal Visible As Boolean, ByVal Length As Integer) As Integer
            Dim retValue As Integer = Null.NullInteger
            Try
                retValue = CType(SqlHelper.ExecuteScalar(ConnectionString, DatabaseOwner & ObjectQualifier & "AddPropertyDefinition", _
                    GetNull(PortalId), ModuleDefId, DataType, DefaultValue, PropertyCategory, _
                    PropertyName, Required, ValidationExpression, ViewOrder, Visible, Length), Integer)
            Catch ex As SqlException
                'If not a duplicate (throw an Exception)
                retValue = -ex.Number
                If ex.Number <> 2601 Then
                    Throw ex
                End If
            End Try
            Return retValue
        End Function
        Public Overrides Sub DeletePropertyDefinition(ByVal definitionId As Integer)
            SqlHelper.ExecuteNonQuery(ConnectionString, DatabaseOwner & ObjectQualifier & "DeletePropertyDefinition", definitionId)
        End Sub
        Public Overrides Function GetPropertyDefinition(ByVal definitionId As Integer) As IDataReader
            Return CType(SqlHelper.ExecuteReader(ConnectionString, DatabaseOwner & ObjectQualifier & "GetPropertyDefinition", definitionId), IDataReader)
        End Function
        Public Overrides Function GetPropertyDefinitionByName(ByVal portalId As Integer, ByVal name As String) As IDataReader
            Return CType(SqlHelper.ExecuteReader(ConnectionString, DatabaseOwner & ObjectQualifier & "GetPropertyDefinitionByName", GetNull(portalId), name), IDataReader)
        End Function
        Public Overrides Function GetPropertyDefinitionsByPortal(ByVal portalId As Integer) As IDataReader
            Return CType(SqlHelper.ExecuteReader(ConnectionString, DatabaseOwner & ObjectQualifier & "GetPropertyDefinitionsByPortal", GetNull(portalId)), IDataReader)
        End Function
        Public Overrides Sub UpdatePropertyDefinition(ByVal PropertyDefinitionId As Integer, ByVal DataType As Integer, ByVal DefaultValue As String, ByVal PropertyCategory As String, ByVal PropertyName As String, ByVal Required As Boolean, ByVal ValidationExpression As String, ByVal ViewOrder As Integer, ByVal Visible As Boolean, ByVal Length As Integer)
            SqlHelper.ExecuteNonQuery(ConnectionString, DatabaseOwner & ObjectQualifier & "UpdatePropertyDefinition", _
                PropertyDefinitionId, DataType, DefaultValue, PropertyCategory, _
                PropertyName, Required, ValidationExpression, ViewOrder, Visible, Length)
        End Sub

        ' urls
        Public Overrides Function GetUrls(ByVal PortalID As Integer) As IDataReader
            Return CType(SqlHelper.ExecuteReader(ConnectionString, DatabaseOwner & ObjectQualifier & "GetUrls", PortalID), IDataReader)
        End Function
        Public Overrides Function GetUrl(ByVal PortalID As Integer, ByVal Url As String) As IDataReader
            Return CType(SqlHelper.ExecuteReader(ConnectionString, DatabaseOwner & ObjectQualifier & "GetUrl", PortalID, Url), IDataReader)
        End Function
        Public Overrides Sub AddUrl(ByVal PortalID As Integer, ByVal Url As String)
            SqlHelper.ExecuteNonQuery(ConnectionString, DatabaseOwner & ObjectQualifier & "AddUrl", PortalID, Url)
        End Sub
        Public Overrides Sub DeleteUrl(ByVal PortalID As Integer, ByVal Url As String)
            SqlHelper.ExecuteNonQuery(ConnectionString, DatabaseOwner & ObjectQualifier & "DeleteUrl", PortalID, Url)
        End Sub
        Public Overrides Function GetUrlTracking(ByVal PortalID As Integer, ByVal Url As String, ByVal ModuleID As Integer) As IDataReader
            Return CType(SqlHelper.ExecuteReader(ConnectionString, DatabaseOwner & ObjectQualifier & "GetUrlTracking", PortalID, Url, GetNull(ModuleID)), IDataReader)
        End Function
        Public Overrides Sub AddUrlTracking(ByVal PortalID As Integer, ByVal Url As String, ByVal UrlType As String, ByVal LogActivity As Boolean, ByVal TrackClicks As Boolean, ByVal ModuleID As Integer, ByVal NewWindow As Boolean)
            SqlHelper.ExecuteNonQuery(ConnectionString, DatabaseOwner & ObjectQualifier & "AddUrlTracking", PortalID, Url, UrlType, LogActivity, TrackClicks, GetNull(ModuleID), NewWindow)
        End Sub
        Public Overrides Sub UpdateUrlTracking(ByVal PortalID As Integer, ByVal Url As String, ByVal LogActivity As Boolean, ByVal TrackClicks As Boolean, ByVal ModuleID As Integer, ByVal NewWindow As Boolean)
            SqlHelper.ExecuteNonQuery(ConnectionString, DatabaseOwner & ObjectQualifier & "UpdateUrlTracking", PortalID, Url, LogActivity, TrackClicks, GetNull(ModuleID), NewWindow)
        End Sub
        Public Overrides Sub DeleteUrlTracking(ByVal PortalID As Integer, ByVal Url As String, ByVal ModuleID As Integer)
            SqlHelper.ExecuteNonQuery(ConnectionString, DatabaseOwner & ObjectQualifier & "DeleteUrlTracking", PortalID, Url, GetNull(ModuleID))
        End Sub
        Public Overrides Sub UpdateUrlTrackingStats(ByVal PortalID As Integer, ByVal Url As String, ByVal ModuleID As Integer)
            SqlHelper.ExecuteNonQuery(ConnectionString, DatabaseOwner & ObjectQualifier & "UpdateUrlTrackingStats", PortalID, Url, GetNull(ModuleID))
        End Sub
        Public Overrides Function GetUrlLog(ByVal UrlTrackingID As Integer, ByVal StartDate As Date, ByVal EndDate As Date) As IDataReader
            Return CType(SqlHelper.ExecuteReader(ConnectionString, DatabaseOwner & ObjectQualifier & "GetUrlLog", UrlTrackingID, GetNull(StartDate), GetNull(EndDate)), IDataReader)
        End Function
        Public Overrides Sub AddUrlLog(ByVal UrlTrackingID As Integer, ByVal UserID As Integer)
            SqlHelper.ExecuteNonQuery(ConnectionString, DatabaseOwner & ObjectQualifier & "AddUrlLog", UrlTrackingID, GetNull(UserID))
        End Sub

        'Permission
        Public Overrides Function GetPermissionsByModuleDefID(ByVal ModuleDefID As Integer) As IDataReader
            Return CType(SqlHelper.ExecuteReader(ConnectionString, DatabaseOwner & ObjectQualifier & "GetPermissionsByModuleDefID", ModuleDefID), IDataReader)
        End Function
        Public Overrides Function GetPermissionsByModuleID(ByVal ModuleID As Integer) As IDataReader
            Return CType(SqlHelper.ExecuteReader(ConnectionString, DatabaseOwner & ObjectQualifier & "GetPermissionsByModuleID", ModuleID), IDataReader)
        End Function
        Public Overrides Function GetPermissionsByFolderPath(ByVal PortalID As Integer, ByVal Folder As String) As IDataReader
            Return CType(SqlHelper.ExecuteReader(ConnectionString, DatabaseOwner & ObjectQualifier & "GetPermissionsByFolderPath", GetNull(PortalID), Folder), IDataReader)
        End Function
        Public Overrides Function GetPermissionByCodeAndKey(ByVal PermissionCode As String, ByVal PermissionKey As String) As IDataReader
            Return CType(SqlHelper.ExecuteReader(ConnectionString, DatabaseOwner & ObjectQualifier & "GetPermissionByCodeAndKey", GetNull(PermissionCode), GetNull(PermissionKey)), IDataReader)
        End Function
        Public Overrides Function GetPermissionsByTabID(ByVal TabID As Integer) As IDataReader
            Return CType(SqlHelper.ExecuteReader(ConnectionString, DatabaseOwner & ObjectQualifier & "GetPermissionsByTabID", TabID), IDataReader)
        End Function
        Public Overrides Function GetPermission(ByVal permissionID As Integer) As IDataReader
            Return CType(SqlHelper.ExecuteReader(ConnectionString, DatabaseOwner & ObjectQualifier & "GetPermission", permissionID), IDataReader)
        End Function
        Public Overrides Sub DeletePermission(ByVal permissionID As Integer)
            SqlHelper.ExecuteNonQuery(ConnectionString, DatabaseOwner & ObjectQualifier & "DeletePermission", permissionID)
        End Sub
        Public Overrides Function AddPermission(ByVal permissionCode As String, ByVal moduleDefID As Integer, ByVal permissionKey As String, ByVal permissionName As String) As Integer
            Return CType(SqlHelper.ExecuteScalar(ConnectionString, DatabaseOwner & ObjectQualifier & "AddPermission", moduleDefID, permissionCode, permissionKey, permissionName), Integer)
        End Function
        Public Overrides Sub UpdatePermission(ByVal permissionID As Integer, ByVal permissionCode As String, ByVal moduleDefID As Integer, ByVal permissionKey As String, ByVal permissionName As String)
            SqlHelper.ExecuteNonQuery(ConnectionString, DatabaseOwner & ObjectQualifier & "UpdatePermission", permissionID, permissionCode, moduleDefID, permissionKey, permissionName)
        End Sub

        'ModulePermission
        Public Overrides Function GetModulePermission(ByVal modulePermissionID As Integer) As IDataReader
            Return CType(SqlHelper.ExecuteReader(ConnectionString, DatabaseOwner & ObjectQualifier & "GetModulePermission", modulePermissionID), IDataReader)
        End Function
        Public Overrides Function GetModulePermissionsByModuleID(ByVal moduleID As Integer, ByVal PermissionID As Integer) As IDataReader
            Return CType(SqlHelper.ExecuteReader(ConnectionString, DatabaseOwner & ObjectQualifier & "GetModulePermissionsByModuleID", moduleID, PermissionID), IDataReader)
        End Function
        Public Overrides Function GetModulePermissionsByPortal(ByVal PortalID As Integer) As IDataReader
            Return CType(SqlHelper.ExecuteReader(ConnectionString, DatabaseOwner & ObjectQualifier & "GetModulePermissionsByPortal", PortalID), IDataReader)
        End Function
        Public Overrides Function GetModulePermissionsByTabID(ByVal TabID As Integer) As IDataReader
            Return CType(SqlHelper.ExecuteReader(ConnectionString, DatabaseOwner & ObjectQualifier & "GetModulePermissionsByTabID", TabID), IDataReader)
        End Function
        Public Overrides Sub DeleteModulePermissionsByModuleID(ByVal ModuleID As Integer)
            SqlHelper.ExecuteNonQuery(ConnectionString, DatabaseOwner & ObjectQualifier & "DeleteModulePermissionsByModuleID", ModuleID)
        End Sub
        Public Overrides Sub DeleteModulePermissionsByUserID(ByVal PortalID As Integer, ByVal UserID As Integer)
            SqlHelper.ExecuteNonQuery(ConnectionString, DatabaseOwner & ObjectQualifier & "DeleteModulePermissionsByUserID", PortalID, UserID)
        End Sub
        Public Overrides Sub DeleteModulePermission(ByVal modulePermissionID As Integer)
            SqlHelper.ExecuteNonQuery(ConnectionString, DatabaseOwner & ObjectQualifier & "DeleteModulePermission", modulePermissionID)
        End Sub
        Public Overrides Function AddModulePermission(ByVal moduleID As Integer, ByVal PermissionID As Integer, ByVal roleID As Integer, ByVal AllowAccess As Boolean, ByVal UserID As Integer) As Integer
            Return CType(SqlHelper.ExecuteScalar(ConnectionString, DatabaseOwner & ObjectQualifier & "AddModulePermission", moduleID, PermissionID, GetRoleNull(roleID), AllowAccess, GetNull(UserID)), Integer)
        End Function
        Public Overrides Sub UpdateModulePermission(ByVal modulePermissionID As Integer, ByVal moduleID As Integer, ByVal PermissionID As Integer, ByVal roleID As Integer, ByVal AllowAccess As Boolean, ByVal UserID As Integer)
            SqlHelper.ExecuteNonQuery(ConnectionString, DatabaseOwner & ObjectQualifier & "UpdateModulePermission", modulePermissionID, moduleID, PermissionID, GetRoleNull(roleID), AllowAccess, GetNull(UserID))
        End Sub


        'TabPermission
        Public Overrides Function GetTabPermissionsByPortal(ByVal PortalID As Integer) As IDataReader
            Return CType(SqlHelper.ExecuteReader(ConnectionString, DatabaseOwner & ObjectQualifier & "GetTabPermissionsByPortal", GetNull(PortalID)), IDataReader)
        End Function
        Public Overrides Function GetTabPermissionsByTabID(ByVal TabID As Integer, ByVal PermissionID As Integer) As IDataReader
            Return CType(SqlHelper.ExecuteReader(ConnectionString, DatabaseOwner & ObjectQualifier & "GetTabPermissionsByTabID", TabID, PermissionID), IDataReader)
        End Function
        Public Overrides Sub DeleteTabPermissionsByTabID(ByVal TabID As Integer)
            SqlHelper.ExecuteNonQuery(ConnectionString, DatabaseOwner & ObjectQualifier & "DeleteTabPermissionsByTabID", TabID)
        End Sub
        Public Overrides Sub DeleteTabPermissionsByUserID(ByVal PortalID As Integer, ByVal UserID As Integer)
            SqlHelper.ExecuteNonQuery(ConnectionString, DatabaseOwner & ObjectQualifier & "DeleteTabPermissionsByUserID", PortalID, UserID)
        End Sub
        Public Overrides Sub DeleteTabPermission(ByVal TabPermissionID As Integer)
            SqlHelper.ExecuteNonQuery(ConnectionString, DatabaseOwner & ObjectQualifier & "DeleteTabPermission", TabPermissionID)
        End Sub
        Public Overrides Function AddTabPermission(ByVal TabID As Integer, ByVal PermissionID As Integer, ByVal roleID As Integer, ByVal AllowAccess As Boolean, ByVal UserID As Integer) As Integer
            Return CType(SqlHelper.ExecuteScalar(ConnectionString, DatabaseOwner & ObjectQualifier & "AddTabPermission", TabID, PermissionID, GetRoleNull(roleID), AllowAccess, GetNull(UserID)), Integer)
        End Function
        Public Overrides Sub UpdateTabPermission(ByVal TabPermissionID As Integer, ByVal TabID As Integer, ByVal PermissionID As Integer, ByVal roleID As Integer, ByVal AllowAccess As Boolean, ByVal UserID As Integer)
            SqlHelper.ExecuteNonQuery(ConnectionString, DatabaseOwner & ObjectQualifier & "UpdateTabPermission", TabPermissionID, TabID, PermissionID, GetRoleNull(roleID), AllowAccess, GetNull(UserID))
        End Sub

        'Folders
        Public Overrides Function GetFoldersByPortal(ByVal PortalID As Integer) As IDataReader
            Return CType(SqlHelper.ExecuteReader(ConnectionString, DatabaseOwner & ObjectQualifier & "GetFolders", GetNull(PortalID), -1, ""), IDataReader)
        End Function
        Public Overloads Overrides Function GetFolder(ByVal PortalID As Integer, ByVal FolderID As Integer) As IDataReader
            Return CType(SqlHelper.ExecuteReader(ConnectionString, DatabaseOwner & ObjectQualifier & "GetFolderByFolderID", GetNull(PortalID), FolderID), IDataReader)
        End Function
        Public Overloads Overrides Function GetFolder(ByVal PortalID As Integer, ByVal FolderPath As String) As IDataReader
            Return CType(SqlHelper.ExecuteReader(ConnectionString, DatabaseOwner & ObjectQualifier & "GetFolderByFolderPath", GetNull(PortalID), FolderPath), IDataReader)
        End Function
        Public Overrides Function AddFolder(ByVal PortalID As Integer, ByVal FolderPath As String, ByVal StorageLocation As Integer, ByVal IsProtected As Boolean, ByVal IsCached As Boolean, ByVal LastUpdated As Date) As Integer
            Return CType(SqlHelper.ExecuteScalar(ConnectionString, DatabaseOwner & ObjectQualifier & "AddFolder", GetNull(PortalID), FolderPath, StorageLocation, IsProtected, IsCached, GetNull(LastUpdated)), Integer)
        End Function
        Public Overrides Sub UpdateFolder(ByVal PortalID As Integer, ByVal FolderID As Integer, ByVal FolderPath As String, ByVal StorageLocation As Integer, ByVal IsProtected As Boolean, ByVal IsCached As Boolean, ByVal LastUpdated As Date)
            SqlHelper.ExecuteScalar(ConnectionString, DatabaseOwner & ObjectQualifier & "UpdateFolder", GetNull(PortalID), FolderID, FolderPath, StorageLocation, IsProtected, IsCached, GetNull(LastUpdated))
        End Sub
        Public Overrides Sub DeleteFolder(ByVal PortalID As Integer, ByVal FolderPath As String)
            SqlHelper.ExecuteScalar(ConnectionString, DatabaseOwner & ObjectQualifier & "DeleteFolder", GetNull(PortalID), FolderPath)
        End Sub

        'FolderPermission
        Public Overrides Function GetFolderPermission(ByVal FolderPermissionID As Integer) As IDataReader
            Return CType(SqlHelper.ExecuteReader(ConnectionString, DatabaseOwner & ObjectQualifier & "GetFolderPermission", FolderPermissionID), IDataReader)
        End Function
        Public Overrides Function GetFolderPermissionsByPortal(ByVal PortalID As Integer) As IDataReader
            Return CType(SqlHelper.ExecuteReader(ConnectionString, DatabaseOwner & ObjectQualifier & "GetFolderPermissionsByPortal", GetNull(PortalID)), IDataReader)
        End Function
        Public Overrides Function GetFolderPermissionsByFolderPath(ByVal PortalID As Integer, ByVal FolderPath As String, ByVal PermissionID As Integer) As IDataReader
            Return CType(SqlHelper.ExecuteReader(ConnectionString, DatabaseOwner & ObjectQualifier & "GetFolderPermissionsByFolderPath", GetNull(PortalID), FolderPath, PermissionID), IDataReader)
        End Function
        Public Overrides Sub DeleteFolderPermissionsByFolderPath(ByVal PortalID As Integer, ByVal FolderPath As String)
            SqlHelper.ExecuteNonQuery(ConnectionString, DatabaseOwner & ObjectQualifier & "DeleteFolderPermissionsByFolderPath", GetNull(PortalID), FolderPath)
        End Sub
        Public Overrides Sub DeleteFolderPermissionsByUserID(ByVal PortalID As Integer, ByVal UserID As Integer)
            SqlHelper.ExecuteNonQuery(ConnectionString, DatabaseOwner & ObjectQualifier & "DeleteFolderPermissionsByUserID", PortalID, UserID)
        End Sub
        Public Overrides Sub DeleteFolderPermission(ByVal FolderPermissionID As Integer)
            SqlHelper.ExecuteNonQuery(ConnectionString, DatabaseOwner & ObjectQualifier & "DeleteFolderPermission", FolderPermissionID)
        End Sub
        Public Overrides Function AddFolderPermission(ByVal FolderID As Integer, ByVal PermissionID As Integer, ByVal roleID As Integer, ByVal AllowAccess As Boolean, ByVal UserID As Integer) As Integer
            Return CType(SqlHelper.ExecuteScalar(ConnectionString, DatabaseOwner & ObjectQualifier & "AddFolderPermission", FolderID, PermissionID, GetRoleNull(roleID), AllowAccess, GetNull(UserID)), Integer)
        End Function
        Public Overrides Sub UpdateFolderPermission(ByVal FolderPermissionID As Integer, ByVal FolderID As Integer, ByVal PermissionID As Integer, ByVal roleID As Integer, ByVal AllowAccess As Boolean, ByVal UserID As Integer)
            SqlHelper.ExecuteNonQuery(ConnectionString, DatabaseOwner & ObjectQualifier & "UpdateFolderPermission", FolderPermissionID, FolderID, PermissionID, GetRoleNull(roleID), AllowAccess, GetNull(UserID))
        End Sub

        ' search engine
        Public Overrides Function GetSearchIndexers() As System.Data.IDataReader
            Return CType(SqlHelper.ExecuteReader(ConnectionString, DatabaseOwner & ObjectQualifier & "GetSearchIndexers"), IDataReader)
        End Function
        Public Overrides Function GetSearchResultModules(ByVal PortalID As Integer) As System.Data.IDataReader
            Return CType(SqlHelper.ExecuteReader(ConnectionString, DatabaseOwner & ObjectQualifier & "GetSearchResultModules", PortalID), IDataReader)
        End Function


        ' content search datastore
        Public Overrides Sub DeleteSearchItems(ByVal ModuleID As Integer)
            SqlHelper.ExecuteNonQuery(ConnectionString, DatabaseOwner & ObjectQualifier & "DeleteSearchItems", ModuleID)
        End Sub
        Public Overrides Sub DeleteSearchItem(ByVal SearchItemId As Integer)
            SqlHelper.ExecuteNonQuery(ConnectionString, DatabaseOwner & ObjectQualifier & "DeleteSearchItem", SearchItemId)
        End Sub
        Public Overrides Sub DeleteSearchItemWords(ByVal SearchItemId As Integer)
            SqlHelper.ExecuteNonQuery(ConnectionString, DatabaseOwner & ObjectQualifier & "DeleteSearchItemWords", SearchItemId)
        End Sub
        Public Overrides Function AddSearchItem(ByVal Title As String, ByVal Description As String, ByVal Author As Integer, ByVal PubDate As Date, ByVal ModuleId As Integer, ByVal Key As String, ByVal Guid As String, ByVal ImageFileId As Integer) As Integer
            Return CType(SqlHelper.ExecuteScalar(ConnectionString, DatabaseOwner & ObjectQualifier & "AddSearchItem", Title, Description, GetNull(Author), GetNull(PubDate), ModuleId, Key, Guid, ImageFileId), Integer)
        End Function
        Public Overrides Function GetSearchCommonWordsByLocale(ByVal Locale As String) As System.Data.IDataReader
            Return CType(SqlHelper.ExecuteReader(ConnectionString, DatabaseOwner & ObjectQualifier & "GetSearchCommonWordsByLocale", Locale), IDataReader)
        End Function
        Public Overrides Function GetDefaultLanguageByModule(ByVal ModuleList As String) As System.Data.IDataReader
            Return CType(SqlHelper.ExecuteReader(ConnectionString, DatabaseOwner & ObjectQualifier & "GetDefaultLanguageByModule", ModuleList), IDataReader)
        End Function
        Public Overrides Function GetSearchSettings(ByVal ModuleId As Integer) As IDataReader
            Return CType(SqlHelper.ExecuteReader(ConnectionString, DatabaseOwner & ObjectQualifier & "GetSearchSettings", ModuleId), IDataReader)
        End Function
        Public Overrides Function GetSearchWords() As System.Data.IDataReader
            Return CType(SqlHelper.ExecuteReader(ConnectionString, DatabaseOwner & ObjectQualifier & "GetSearchWords"), IDataReader)
        End Function
        Public Overrides Function AddSearchWord(ByVal Word As String) As Integer
            Return CType(SqlHelper.ExecuteScalar(ConnectionString, DatabaseOwner & ObjectQualifier & "AddSearchWord", Word), Integer)
        End Function

        Public Overrides Function AddSearchItemWord(ByVal SearchItemId As Integer, ByVal SearchWordsID As Integer, ByVal Occurrences As Integer) As Integer
            Return CType(SqlHelper.ExecuteScalar(ConnectionString, DatabaseOwner & ObjectQualifier & "AddSearchItemWord", SearchItemId, SearchWordsID, Occurrences), Integer)
        End Function
        Public Overrides Sub AddSearchItemWordPosition(ByVal SearchItemWordID As Integer, ByVal ContentPositions As String)
            SqlHelper.ExecuteNonQuery(ConnectionString, DatabaseOwner & ObjectQualifier & "AddSearchItemWordPosition", SearchItemWordID, ContentPositions)
        End Sub
        Public Overrides Function GetSearchResults(ByVal PortalID As Integer, ByVal Word As String) As IDataReader
            Return CType(SqlHelper.ExecuteReader(ConnectionString, DatabaseOwner & ObjectQualifier & "GetSearchResults", PortalID, Word), IDataReader)
        End Function
        Public Overrides Function GetSearchItems(ByVal PortalID As Integer, ByVal TabID As Integer, ByVal ModuleID As Integer) As IDataReader
            Return CType(SqlHelper.ExecuteReader(ConnectionString, DatabaseOwner & ObjectQualifier & "GetSearchItems", GetNull(PortalID), GetNull(TabID), GetNull(ModuleID)), IDataReader)
        End Function
        Public Overrides Function GetSearchItem(ByVal ModuleID As Integer, ByVal SearchKey As String) As IDataReader
            Return CType(SqlHelper.ExecuteReader(ConnectionString, DatabaseOwner & ObjectQualifier & "GetSearchItem", GetNull(ModuleID), SearchKey), IDataReader)
        End Function
        Public Overrides Sub UpdateSearchItem(ByVal SearchItemId As Integer, ByVal Title As String, ByVal Description As String, ByVal Author As Integer, ByVal PubDate As Date, ByVal ModuleId As Integer, ByVal Key As String, ByVal Guid As String, ByVal HitCount As Integer, ByVal ImageFileId As Integer)
            SqlHelper.ExecuteScalar(ConnectionString, DatabaseOwner & ObjectQualifier & "UpdateSearchItem", SearchItemId, Title, Description, GetNull(Author), GetNull(PubDate), ModuleId, Key, Guid, HitCount, ImageFileId)
        End Sub

        'Lists
        Public Overrides Function GetLists(ByVal PortalID As Integer) As IDataReader
            Return CType(SqlHelper.ExecuteReader(ConnectionString, DatabaseOwner & ObjectQualifier & "GetLists", PortalID), IDataReader)
        End Function
        Public Overrides Function GetList(ByVal ListName As String, ByVal ParentKey As String, ByVal PortalID As Integer) As IDataReader
            Return CType(SqlHelper.ExecuteReader(ConnectionString, DatabaseOwner & ObjectQualifier & "GetList", ListName, ParentKey, PortalID), IDataReader)
        End Function
        Public Overrides Function GetListEntry(ByVal ListName As String, ByVal Value As String) As IDataReader
            Return CType(SqlHelper.ExecuteReader(ConnectionString, DatabaseOwner & ObjectQualifier & "GetListEntry", ListName, Value, -1), IDataReader)
        End Function
        Public Overrides Function GetListEntry(ByVal EntryID As Integer) As IDataReader
            Return CType(SqlHelper.ExecuteReader(ConnectionString, DatabaseOwner & ObjectQualifier & "GetListEntry", "", "", EntryID), IDataReader)
        End Function
        Public Overrides Function GetListEntriesByListName(ByVal ListName As String, ByVal ParentKey As String) As IDataReader
            Return CType(SqlHelper.ExecuteReader(ConnectionString, DatabaseOwner & ObjectQualifier & "GetListEntries", ListName, ParentKey), IDataReader)
        End Function
        Public Overrides Function AddListEntry(ByVal ListName As String, ByVal Value As String, ByVal Text As String, ByVal ParentID As Integer, ByVal Level As Integer, ByVal EnableSortOrder As Boolean, ByVal DefinitionID As Integer, ByVal Description As String, ByVal PortalID As Integer) As Integer
            Return CType(SqlHelper.ExecuteScalar(ConnectionString, DatabaseOwner & ObjectQualifier & "AddListEntry", ListName, Value, Text, ParentID, Level, EnableSortOrder, DefinitionID, Description, PortalID), Integer)
        End Function
        Public Overrides Sub UpdateListEntry(ByVal EntryID As Integer, ByVal Value As String, ByVal Text As String, ByVal Description As String)
            SqlHelper.ExecuteNonQuery(ConnectionString, DatabaseOwner & ObjectQualifier & "UpdateListEntry", EntryID, Value, Text, Description)
        End Sub
        Public Overrides Sub DeleteListEntryByID(ByVal EntryID As Integer, ByVal DeleteChild As Boolean)
            SqlHelper.ExecuteNonQuery(ConnectionString, DatabaseOwner & ObjectQualifier & "DeleteListEntryByID", EntryID, DeleteChild)
        End Sub
        Public Overrides Sub DeleteList(ByVal ListName As String, ByVal ParentKey As String)
            SqlHelper.ExecuteNonQuery(ConnectionString, DatabaseOwner & ObjectQualifier & "DeleteList", ListName, ParentKey)
        End Sub
        Public Overrides Sub DeleteListEntryByListName(ByVal ListName As String, ByVal Value As String, ByVal DeleteChild As Boolean)
            SqlHelper.ExecuteNonQuery(ConnectionString, DatabaseOwner & ObjectQualifier & "DeleteListEntryByListName", ListName, Value, DeleteChild)
        End Sub
        Public Overrides Sub UpdateListSortOrder(ByVal EntryID As Integer, ByVal MoveUp As Boolean)
            SqlHelper.ExecuteNonQuery(ConnectionString, DatabaseOwner & ObjectQualifier & "UpdateListSortOrder", EntryID, MoveUp)
        End Sub

        'portal alias
        Public Overrides Function GetPortalAlias(ByVal PortalAlias As String, ByVal PortalID As Integer) As IDataReader
            Return SqlHelper.ExecuteReader(ConnectionString, DatabaseOwner & ObjectQualifier & "GetPortalAlias", PortalAlias, PortalID)
        End Function
        Public Overrides Function GetPortalByPortalAliasID(ByVal PortalAliasId As Integer) As IDataReader
            Return SqlHelper.ExecuteReader(ConnectionString, DatabaseOwner & ObjectQualifier & "GetPortalByPortalAliasID", PortalAliasId)
        End Function
        Public Overrides Sub UpdatePortalAlias(ByVal PortalAlias As String)
            SqlHelper.ExecuteNonQuery(ConnectionString, DatabaseOwner & ObjectQualifier & "UpdatePortalAliasOnInstall", PortalAlias)
        End Sub
        Public Overrides Sub UpdatePortalAliasInfo(ByVal PortalAliasID As Integer, ByVal PortalID As Integer, ByVal HTTPAlias As String)
            SqlHelper.ExecuteNonQuery(ConnectionString, DatabaseOwner & ObjectQualifier & "UpdatePortalAlias", PortalAliasID, PortalID, HTTPAlias)
        End Sub
        Public Overrides Function AddPortalAlias(ByVal PortalID As Integer, ByVal HTTPAlias As String) As Integer
            Return CType(SqlHelper.ExecuteScalar(ConnectionString, DatabaseOwner & ObjectQualifier & "AddPortalAlias", PortalID, HTTPAlias), Integer)
        End Function
        Public Overrides Sub DeletePortalAlias(ByVal PortalAliasID As Integer)
            SqlHelper.ExecuteNonQuery(ConnectionString, DatabaseOwner & ObjectQualifier & "DeletePortalAlias", PortalAliasID)
        End Sub
        Public Overrides Function GetPortalAliasByPortalID(ByVal PortalID As Integer) As IDataReader
            Return SqlHelper.ExecuteReader(ConnectionString, DatabaseOwner & ObjectQualifier & "GetPortalAliasByPortalID", PortalID)
        End Function
        Public Overrides Function GetPortalAliasByPortalAliasID(ByVal PortalAliasID As Integer) As IDataReader
            Return SqlHelper.ExecuteReader(ConnectionString, DatabaseOwner & ObjectQualifier & "GetPortalAliasByPortalAliasID", PortalAliasID)
        End Function

        'event Queue
        Public Overrides Function AddEventMessage(ByVal eventName As String, ByVal priority As Integer, ByVal processorType As String, ByVal processorCommand As String, ByVal body As String, ByVal sender As String, ByVal subscriberId As String, ByVal authorizedRoles As String, ByVal exceptionMessage As String, ByVal sentDate As Date, ByVal expirationDate As Date, ByVal attributes As String) As Integer
            SqlHelper.ExecuteNonQuery(ConnectionString, DatabaseOwner & ObjectQualifier & "AddEventMessage", eventName, priority, processorType, processorCommand, body, sender, subscriberId, authorizedRoles, exceptionMessage, sentDate, expirationDate, attributes)
        End Function
        Public Overrides Function GetEventMessages(ByVal eventName As String) As IDataReader
            Return SqlHelper.ExecuteReader(ConnectionString, DatabaseOwner & ObjectQualifier & "GetEventMessages", eventName)
        End Function
        Public Overrides Function GetEventMessagesBySubscriber(ByVal eventName As String, ByVal subscriberId As String) As IDataReader
            Return SqlHelper.ExecuteReader(ConnectionString, DatabaseOwner & ObjectQualifier & "GetEventMessagesBySubscriber", eventName, subscriberId)
        End Function
        Public Overrides Sub SetEventMessageComplete(ByVal eventMessageId As Integer)
            SqlHelper.ExecuteNonQuery(ConnectionString, DatabaseOwner & ObjectQualifier & "SetEventMessageComplete", eventMessageId)
        End Sub

        'Authentication
        Public Overrides Function AddAuthentication(ByVal packageID As Integer, ByVal authenticationType As String, ByVal isEnabled As Boolean, ByVal settingsControlSrc As String, ByVal loginControlSrc As String, ByVal logoffControlSrc As String) As Integer
            Return CType(SqlHelper.ExecuteScalar(ConnectionString, DatabaseOwner & ObjectQualifier & "AddAuthentication", packageID, authenticationType, isEnabled, settingsControlSrc, loginControlSrc, logoffControlSrc), Integer)
        End Function
        Public Overrides Function AddUserAuthentication(ByVal userID As Integer, ByVal authenticationType As String, ByVal authenticationToken As String) As Integer
            Return CType(SqlHelper.ExecuteScalar(ConnectionString, DatabaseOwner & ObjectQualifier & "AddUserAuthentication", userID, authenticationType, authenticationToken), Integer)
        End Function
        Public Overrides Sub DeleteAuthentication(ByVal authenticationID As Integer)
            SqlHelper.ExecuteNonQuery(ConnectionString, DatabaseOwner & ObjectQualifier & "DeleteAuthentication", authenticationID)
        End Sub
        Public Overrides Function GetAuthenticationService(ByVal authenticationID As Integer) As IDataReader
            Return SqlHelper.ExecuteReader(ConnectionString, DatabaseOwner & ObjectQualifier & "GetAuthenticationService", authenticationID)
        End Function
        Public Overrides Function GetAuthenticationServiceByPackageID(ByVal packageID As Integer) As IDataReader
            Return SqlHelper.ExecuteReader(ConnectionString, DatabaseOwner & ObjectQualifier & "GetAuthenticationServiceByPackageID", packageID)
        End Function
        Public Overrides Function GetAuthenticationServiceByType(ByVal authenticationType As String) As IDataReader
            Return SqlHelper.ExecuteReader(ConnectionString, DatabaseOwner & ObjectQualifier & "GetAuthenticationServiceByType", authenticationType)
        End Function
        Public Overrides Function GetAuthenticationServices() As IDataReader
            Return SqlHelper.ExecuteReader(ConnectionString, DatabaseOwner & ObjectQualifier & "GetAuthenticationServices")
        End Function
        Public Overrides Function GetEnabledAuthenticationServices() As IDataReader
            Return SqlHelper.ExecuteReader(ConnectionString, DatabaseOwner & ObjectQualifier & "GetEnabledAuthenticationServices")
        End Function
        Public Overrides Sub UpdateAuthentication(ByVal authenticationID As Integer, ByVal packageID As Integer, ByVal authenticationType As String, ByVal isEnabled As Boolean, ByVal settingsControlSrc As String, ByVal loginControlSrc As String, ByVal logoffControlSrc As String)
            SqlHelper.ExecuteNonQuery(ConnectionString, DatabaseOwner & ObjectQualifier & "UpdateAuthentication", authenticationID, packageID, authenticationType, isEnabled, settingsControlSrc, loginControlSrc, logoffControlSrc)
        End Sub

        'Packages
        Public Overrides Function AddPackage(ByVal name As String, ByVal friendlyName As String, ByVal description As String, ByVal type As String, ByVal version As String, ByVal license As String, ByVal manifest As String) As Integer
            Return CType(SqlHelper.ExecuteScalar(ConnectionString, DatabaseOwner & ObjectQualifier & "AddPackage", name, friendlyName, description, type, version, license, manifest), Integer)
        End Function
        Public Overrides Sub DeletePackage(ByVal packageID As Integer)
            SqlHelper.ExecuteNonQuery(ConnectionString, DatabaseOwner & ObjectQualifier & "DeletePackage", packageID)
        End Sub
        Public Overrides Function GetPackage(ByVal packageID As Integer) As IDataReader
            Return SqlHelper.ExecuteReader(ConnectionString, DatabaseOwner & ObjectQualifier & "GetPackage", packageID)
        End Function
        Public Overrides Function GetPackageByName(ByVal name As String) As IDataReader
            Return SqlHelper.ExecuteReader(ConnectionString, DatabaseOwner & ObjectQualifier & "GetPackageByName", name)
        End Function
        Public Overrides Function GetPackages() As IDataReader
            Return SqlHelper.ExecuteReader(ConnectionString, DatabaseOwner & ObjectQualifier & "GetPackages")
        End Function
        Public Overrides Function GetPackagesByType(ByVal type As String) As IDataReader
            Return SqlHelper.ExecuteReader(ConnectionString, DatabaseOwner & ObjectQualifier & "GetPackagesByType", type)
        End Function
        Public Overrides Function GetPackageType(ByVal type As String) As IDataReader
            Return SqlHelper.ExecuteReader(ConnectionString, DatabaseOwner & ObjectQualifier & "GetPackageType", type)
        End Function
        Public Overrides Function RegisterAssembly(ByVal packageID As Integer, ByVal assemblyName As String, ByVal version As String) As Integer
            Return CType(SqlHelper.ExecuteScalar(ConnectionString, DatabaseOwner & ObjectQualifier & "RegisterAssembly", packageID, assemblyName, version), Integer)
        End Function
        Public Overrides Function UnRegisterAssembly(ByVal packageID As Integer, ByVal assemblyName As String) As Boolean
            Return CType(SqlHelper.ExecuteScalar(ConnectionString, DatabaseOwner & ObjectQualifier & "UnRegisterAssembly", packageID, assemblyName), Boolean)
        End Function
        Public Overrides Sub UpdatePackage(ByVal name As String, ByVal friendlyName As String, ByVal description As String, ByVal type As String, ByVal version As String, ByVal license As String, ByVal manifest As String)
            SqlHelper.ExecuteNonQuery(ConnectionString, DatabaseOwner & ObjectQualifier & "UpdatePackage", name, friendlyName, description, type, version, license, manifest)
        End Sub

#End Region

    End Class

End Namespace
