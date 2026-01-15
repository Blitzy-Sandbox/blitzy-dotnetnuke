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
Imports System.Data.SqlTypes
Imports Microsoft.ApplicationBlocks.Data
Imports DotNetNuke
Imports DotNetNuke.Common
Imports DotNetNuke.Common.Utilities


Namespace DotNetNuke.Services.Scheduling.DNNScheduling

    Public Class SqlDataProvider
        Inherits Services.Scheduling.DNNScheduling.DataProvider

        Private Const ProviderType As String = "data"

        Private _providerConfiguration As Framework.Providers.ProviderConfiguration = Framework.Providers.ProviderConfiguration.GetProviderConfiguration(ProviderType)
        Private _connectionString As String
        Private _providerPath As String
        Private _objectQualifier As String
        Private _databaseOwner As String

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

        ' general
        Private Function FixDate(ByVal dateToFix As DateTime) As DateTime
            'Fix for Sql Dates having a minimum value of 1/1/1753
            If dateToFix < SqlDateTime.MinValue.Value Then
                dateToFix = SqlDateTime.MinValue.Value
            End If
            Return dateToFix
        End Function

        Private Function GetNull(ByVal Field As Object) As Object
            Dim nullValue As Object = Null.GetNull(Field, DBNull.Value)
            If TypeOf nullValue Is DateTime Then
                nullValue = FixDate(CType(nullValue, DateTime))
            End If
            Return nullValue
        End Function

        Public Overloads Overrides Function GetSchedule() As IDataReader
            Return CType(SqlHelper.ExecuteReader(ConnectionString, DatabaseOwner & ObjectQualifier & "GetSchedule", DBNull.Value), IDataReader)
        End Function
        Public Overloads Overrides Function GetSchedule(ByVal Server As String) As IDataReader
            Return CType(SqlHelper.ExecuteReader(ConnectionString, DatabaseOwner & ObjectQualifier & "GetSchedule", GetNull(Server)), IDataReader)
        End Function
        Public Overloads Overrides Function GetSchedule(ByVal ScheduleID As Integer) As IDataReader
            Return CType(SqlHelper.ExecuteReader(ConnectionString, DatabaseOwner & ObjectQualifier & "GetScheduleByScheduleID", ScheduleID), IDataReader)
        End Function
        Public Overloads Overrides Function GetSchedule(ByVal TypeFullName As String, ByVal Server As String) As IDataReader
            Return CType(SqlHelper.ExecuteReader(ConnectionString, DatabaseOwner & ObjectQualifier & "GetScheduleByTypeFullName", TypeFullName, GetNull(Server)), IDataReader)
        End Function
        Public Overloads Overrides Function GetNextScheduledTask(ByVal Server As String) As IDataReader
            Return CType(SqlHelper.ExecuteReader(ConnectionString, DatabaseOwner & ObjectQualifier & "GetScheduleNextTask", GetNull(Server)), IDataReader)
        End Function
        Public Overloads Overrides Function GetScheduleByEvent(ByVal EventName As String, ByVal Server As String) As IDataReader
            Return CType(SqlHelper.ExecuteReader(ConnectionString, DatabaseOwner & ObjectQualifier & "GetScheduleByEvent", EventName, GetNull(Server)), IDataReader)
        End Function
        Public Overrides Function GetScheduleHistory(ByVal ScheduleID As Integer) As IDataReader
            Return CType(SqlHelper.ExecuteReader(ConnectionString, DatabaseOwner & ObjectQualifier & "GetScheduleHistory", ScheduleID), IDataReader)
        End Function
        Public Overrides Function AddSchedule(ByVal TypeFullName As String, ByVal TimeLapse As Integer, ByVal TimeLapseMeasurement As String, ByVal RetryTimeLapse As Integer, ByVal RetryTimeLapseMeasurement As String, ByVal RetainHistoryNum As Integer, ByVal AttachToEvent As String, ByVal CatchUpEnabled As Boolean, ByVal Enabled As Boolean, ByVal ObjectDependencies As String, ByVal Servers As String) As Integer
            Return CType(SqlHelper.ExecuteScalar(ConnectionString, DatabaseOwner & ObjectQualifier & "AddSchedule", TypeFullName, TimeLapse, TimeLapseMeasurement, RetryTimeLapse, RetryTimeLapseMeasurement, RetainHistoryNum, AttachToEvent, CatchUpEnabled, Enabled, ObjectDependencies, GetNull(Servers)), Integer)
        End Function
        Public Overrides Sub UpdateSchedule(ByVal ScheduleID As Integer, ByVal TypeFullName As String, ByVal TimeLapse As Integer, ByVal TimeLapseMeasurement As String, ByVal RetryTimeLapse As Integer, ByVal RetryTimeLapseMeasurement As String, ByVal RetainHistoryNum As Integer, ByVal AttachToEvent As String, ByVal CatchUpEnabled As Boolean, ByVal Enabled As Boolean, ByVal ObjectDependencies As String, ByVal Servers As String)
            SqlHelper.ExecuteNonQuery(ConnectionString, DatabaseOwner & ObjectQualifier & "UpdateSchedule", ScheduleID, TypeFullName, TimeLapse, TimeLapseMeasurement, RetryTimeLapse, RetryTimeLapseMeasurement, RetainHistoryNum, AttachToEvent, CatchUpEnabled, Enabled, ObjectDependencies, GetNull(Servers))
        End Sub
        Public Overrides Sub DeleteSchedule(ByVal ScheduleID As Integer)
            SqlHelper.ExecuteNonQuery(ConnectionString, DatabaseOwner & ObjectQualifier & "DeleteSchedule", ScheduleID)
        End Sub
        Public Overrides Function GetScheduleItemSettings(ByVal ScheduleID As Integer) As IDataReader
            Return CType(SqlHelper.ExecuteReader(ConnectionString, DatabaseOwner & ObjectQualifier & "GetScheduleItemSettings", ScheduleID), IDataReader)
        End Function
        Public Overrides Sub AddScheduleItemSetting(ByVal ScheduleID As Integer, ByVal Name As String, ByVal Value As String)
            SqlHelper.ExecuteNonQuery(ConnectionString, DatabaseOwner & ObjectQualifier & "AddScheduleItemSetting", ScheduleID, Name, Value)
        End Sub
        Public Overrides Function AddScheduleHistory(ByVal ScheduleID As Integer, ByVal StartDate As Date, ByVal Server As String) As Integer
            Return CType(SqlHelper.ExecuteScalar(ConnectionString, DatabaseOwner & ObjectQualifier & "AddScheduleHistory", ScheduleID, FixDate(StartDate), Server), Integer)
        End Function
        Public Overrides Sub UpdateScheduleHistory(ByVal ScheduleHistoryID As Integer, ByVal EndDate As Date, ByVal Succeeded As Boolean, ByVal LogNotes As String, ByVal NextStart As Date)
            SqlHelper.ExecuteNonQuery(ConnectionString, DatabaseOwner & ObjectQualifier & "UpdateScheduleHistory", ScheduleHistoryID, GetNull(EndDate), GetNull(Succeeded), LogNotes, GetNull(NextStart))
        End Sub
        Public Overrides Sub PurgeScheduleHistory()
            SqlHelper.ExecuteNonQuery(ConnectionString, DatabaseOwner & ObjectQualifier & "PurgeScheduleHistory")
        End Sub




    End Class

End Namespace