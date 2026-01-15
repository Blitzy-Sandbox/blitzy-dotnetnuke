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
Imports System.Data.SqlClient
Imports Microsoft.ApplicationBlocks.Data
Imports DotNetNuke
Imports DotNetNuke.Services.Log.EventLog.DBLoggingProvider


Namespace DotNetNuke.Services.Log.EventLog.DBLoggingProvider.Data

    Public Class SqlDataProvider
        Inherits DataProvider

#Region "Private Members"
        Private Const ProviderType As String = "data"
        Private _providerConfiguration As Framework.Providers.ProviderConfiguration = Framework.Providers.ProviderConfiguration.GetProviderConfiguration(ProviderType)
        Private _connectionString As String
        Private _providerPath As String
        Private _objectQualifier As String
        Private _databaseOwner As String
#End Region

#Region "Constructors"
        Public Sub New()

            ' Read the configuration specific information for this provider
            Dim objProvider As Framework.Providers.Provider = CType(_providerConfiguration.Providers(_providerConfiguration.DefaultProvider), Framework.Providers.Provider)

            ' Read the attributes for this provider
            'Get Connection string from web.config
            _connectionString = Config.GetConnectionString()

            If _connectionString = "" Then
                ' Use connection string specified in provider
                _connectionString = objProvider.Attributes("connectionString")
            End If

            _providerPath = objProvider.Attributes("providerPath")

            _objectQualifier = objProvider.Attributes("objectQualifier")
            If _objectQualifier <> "" And _objectQualifier.EndsWith("_") = False Then
                _objectQualifier += "_"
            End If

            _databaseOwner = objProvider.Attributes("databaseOwner")
            If _databaseOwner <> "" And _databaseOwner.EndsWith(".") = False Then
                _databaseOwner += "."
            End If

        End Sub
#End Region

#Region "Properties"
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
#End Region

#Region "General Public Methods"
        Private Function GetNull(ByVal Field As Object) As Object
            Return DotNetNuke.Common.Utilities.Null.GetNull(Field, DBNull.Value)
        End Function
#End Region

#Region "DBLoggingProviderSqlDataProvider Methods"
        Public Overrides Sub AddLog(ByVal LogGUID As String, ByVal LogTypeKey As String, ByVal LogUserID As Integer, ByVal LogUserName As String, ByVal LogPortalID As Integer, ByVal LogPortalName As String, ByVal LogCreateDate As Date, ByVal LogServerName As String, ByVal LogProperties As String, ByVal LogConfigID As Integer)
            SqlHelper.ExecuteNonQuery(ConnectionString, DatabaseOwner & ObjectQualifier & "AddEventLog", LogGUID, LogTypeKey, GetNull(LogUserID), GetNull(LogUserName), GetNull(LogPortalID), GetNull(LogPortalName), LogCreateDate, LogServerName, LogProperties, LogConfigID)
        End Sub

        Public Overrides Sub AddLogTypeConfigInfo(ByVal LoggingIsActive As Boolean, ByVal LogTypeKey As String, ByVal LogTypePortalID As String, ByVal KeepMostRecent As Integer, ByVal EmailNotificationIsActive As Boolean, ByVal Threshold As Integer, ByVal NotificationThresholdTime As Integer, ByVal NotificationThresholdTimeType As Integer, ByVal MailFromAddress As String, ByVal MailToAddress As String)
            Dim PortalID As Integer
            If LogTypeKey = "*" Then LogTypeKey = ""
            If LogTypePortalID = "*" Then
                PortalID = -1
            Else
                PortalID = Convert.ToInt32(LogTypePortalID)
            End If
            SqlHelper.ExecuteNonQuery(ConnectionString, DatabaseOwner & ObjectQualifier & "AddEventLogConfig", GetNull(LogTypeKey), GetNull(PortalID), LoggingIsActive, KeepMostRecent, EmailNotificationIsActive, GetNull(Threshold), GetNull(NotificationThresholdTime), GetNull(NotificationThresholdTimeType), MailFromAddress, MailToAddress)
        End Sub

        Public Overrides Sub UpdateLogTypeConfigInfo(ByVal ID As String, ByVal LoggingIsActive As Boolean, ByVal LogTypeKey As String, ByVal LogTypePortalID As String, ByVal KeepMostRecent As Integer, ByVal LogFileName As String, ByVal EmailNotificationIsActive As Boolean, ByVal Threshold As Integer, ByVal NotificationThresholdTime As Integer, ByVal NotificationThresholdTimeType As Integer, ByVal MailFromAddress As String, ByVal MailToAddress As String)
            Dim PortalID As Integer
            If LogTypeKey = "*" Then LogTypeKey = ""
            If LogTypePortalID = "*" Then
                PortalID = -1
            Else
                PortalID = Convert.ToInt32(LogTypePortalID)
            End If
            SqlHelper.ExecuteNonQuery(ConnectionString, DatabaseOwner & ObjectQualifier & "UpdateEventLogConfig", ID, GetNull(LogTypeKey), GetNull(PortalID), LoggingIsActive, KeepMostRecent, EmailNotificationIsActive, GetNull(Threshold), GetNull(NotificationThresholdTime), GetNull(NotificationThresholdTimeType), MailFromAddress, MailToAddress)
        End Sub

        Public Overrides Sub ClearLog()
            SqlHelper.ExecuteNonQuery(ConnectionString, DatabaseOwner & ObjectQualifier & "DeleteEventLog", DBNull.Value)
        End Sub

        Public Overrides Sub DeleteLog(ByVal LogGUID As String)
            SqlHelper.ExecuteNonQuery(ConnectionString, DatabaseOwner & ObjectQualifier & "DeleteEventLog", LogGUID)
        End Sub

        Public Overrides Sub DeleteLogTypeConfigInfo(ByVal ID As String)
            SqlHelper.ExecuteNonQuery(ConnectionString, DatabaseOwner & ObjectQualifier & "DeleteEventLogConfig", ID)
        End Sub

        Public Overloads Overrides Function GetLog() As IDataReader
            Return CType(SqlHelper.ExecuteReader(ConnectionString, DatabaseOwner & ObjectQualifier & "GetEventLog", DBNull.Value, DBNull.Value), IDataReader)
        End Function

        Public Overrides Function GetSingleLog(ByVal LogGUID As String) As IDataReader
            Return CType(SqlHelper.ExecuteReader(ConnectionString, DatabaseOwner & ObjectQualifier & "GetEventLogByLogGUID", LogGUID), IDataReader)
        End Function

        Public Overloads Overrides Function GetLog(ByVal PortalID As Integer) As IDataReader
            Return CType(SqlHelper.ExecuteReader(ConnectionString, DatabaseOwner & ObjectQualifier & "GetEventLog", PortalID, DBNull.Value), IDataReader)
        End Function

        Public Overloads Overrides Function GetLog(ByVal PortalID As Integer, ByVal LogType As String) As IDataReader
            Return CType(SqlHelper.ExecuteReader(ConnectionString, DatabaseOwner & ObjectQualifier & "GetEventLog", PortalID, LogType), IDataReader)
        End Function

        Public Overloads Overrides Function GetLog(ByVal LogType As String) As IDataReader
            Return CType(SqlHelper.ExecuteReader(ConnectionString, DatabaseOwner & ObjectQualifier & "GetEventLog", DBNull.Value, LogType), IDataReader)
        End Function

        Public Overloads Overrides Function GetLog(ByVal PageSize As Integer, ByVal PageIndex As Integer) As IDataReader
            Return CType(SqlHelper.ExecuteReader(ConnectionString, DatabaseOwner & ObjectQualifier & "GetEventLog", DBNull.Value, DBNull.Value, PageSize, PageIndex), IDataReader)
        End Function

        Public Overloads Overrides Function GetLog(ByVal PortalID As Integer, ByVal PageSize As Integer, ByVal PageIndex As Integer) As IDataReader
            Return CType(SqlHelper.ExecuteReader(ConnectionString, DatabaseOwner & ObjectQualifier & "GetEventLog", PortalID, DBNull.Value, PageSize, PageIndex), IDataReader)
        End Function

        Public Overloads Overrides Function GetLog(ByVal PortalID As Integer, ByVal LogType As String, ByVal PageSize As Integer, ByVal PageIndex As Integer) As IDataReader
            Return CType(SqlHelper.ExecuteReader(ConnectionString, DatabaseOwner & ObjectQualifier & "GetEventLog", PortalID, LogType, PageSize, PageIndex), IDataReader)
        End Function

        Public Overloads Overrides Function GetLog(ByVal LogType As String, ByVal PageSize As Integer, ByVal PageIndex As Integer) As IDataReader
            Return CType(SqlHelper.ExecuteReader(ConnectionString, DatabaseOwner & ObjectQualifier & "GetEventLog", DBNull.Value, LogType, PageSize, PageIndex), IDataReader)
        End Function


        Public Overrides Function GetLogTypeConfigInfo() As IDataReader
            Return CType(SqlHelper.ExecuteReader(ConnectionString, DatabaseOwner & ObjectQualifier & "GetEventLogConfig", DBNull.Value), IDataReader)
        End Function

        Public Overrides Function GetLogTypeConfigInfoByID(ByVal ID As Integer) As IDataReader
            Return CType(SqlHelper.ExecuteReader(ConnectionString, DatabaseOwner & ObjectQualifier & "GetEventLogConfig", ID), IDataReader)
        End Function

        Public Overrides Function GetLogTypeInfo() As IDataReader
            Return CType(SqlHelper.ExecuteReader(ConnectionString, DatabaseOwner & ObjectQualifier & "GetEventLogType"), IDataReader)
        End Function


        Public Overrides Sub PurgeLog()
            SqlHelper.ExecuteNonQuery(ConnectionString, DatabaseOwner & ObjectQualifier & "PurgeEventLog")
        End Sub

        Public Overrides Sub AddLogType(ByVal LogTypeKey As String, ByVal LogTypeFriendlyName As String, ByVal LogTypeDescription As String, ByVal LogTypeCSSClass As String, ByVal LogTypeOwner As String)
            SqlHelper.ExecuteNonQuery(ConnectionString, DatabaseOwner & ObjectQualifier & "AddEventLogType", LogTypeKey, LogTypeFriendlyName, LogTypeDescription, LogTypeOwner, LogTypeCSSClass)
        End Sub
        Public Overrides Sub UpdateLogType(ByVal LogTypeKey As String, ByVal LogTypeFriendlyName As String, ByVal LogTypeDescription As String, ByVal LogTypeCSSClass As String, ByVal LogTypeOwner As String)
            SqlHelper.ExecuteNonQuery(ConnectionString, DatabaseOwner & ObjectQualifier & "UpdateEventLogType", LogTypeKey, LogTypeFriendlyName, LogTypeDescription, LogTypeOwner, LogTypeCSSClass)
        End Sub
        Public Overrides Sub DeleteLogType(ByVal LogTypeKey As String)
            SqlHelper.ExecuteNonQuery(ConnectionString, DatabaseOwner & ObjectQualifier & "DeleteEventLogType", LogTypeKey)
        End Sub

        Public Overrides Function GetEventLogPendingNotifConfig() As IDataReader
            Return CType(SqlHelper.ExecuteReader(ConnectionString, DatabaseOwner & ObjectQualifier & "GetEventLogPendingNotifConfig"), IDataReader)
        End Function
        Public Overrides Function GetEventLogPendingNotif(ByVal LogConfigID As Integer) As IDataReader
            Return CType(SqlHelper.ExecuteReader(ConnectionString, DatabaseOwner & ObjectQualifier & "GetEventLogPendingNotif", LogConfigID), IDataReader)
        End Function
        Public Overrides Sub UpdateEventLogPendingNotif(ByVal LogConfigID As Integer)
            SqlHelper.ExecuteNonQuery(ConnectionString, DatabaseOwner & ObjectQualifier & "UpdateEventLogPendingNotif", LogConfigID)
        End Sub




#End Region

    End Class

End Namespace