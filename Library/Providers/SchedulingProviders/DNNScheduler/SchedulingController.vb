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
Imports System.Configuration
Imports System.Data
Imports System.XML
Imports DotNetNuke.Services.Scheduling
Imports DotNetNuke.Common.Utilities

Namespace DotNetNuke.Services.Scheduling.DNNScheduling

	Public Class SchedulingController

        Public Function GetSchedule() As ArrayList
            Dim arrSource As ArrayList = CBO.FillCollection(DataProvider.Instance.GetSchedule(), GetType(ScheduleHistoryItem))
            Dim arrDest As New ArrayList
            Dim i As Integer
            For i = 0 To arrSource.Count - 1
                Dim objScheduleItem As ScheduleItem
                objScheduleItem = CType(arrSource(i), ScheduleItem)
                arrDest.Add(objScheduleItem)
            Next
            Return arrDest
        End Function
        Public Function GetSchedule(ByVal Server As String) As ArrayList
            Dim arrSource As ArrayList = CBO.FillCollection(DataProvider.Instance.GetSchedule(Server), GetType(ScheduleHistoryItem))
            Dim arrDest As New ArrayList
            Dim i As Integer
            For i = 0 To arrSource.Count - 1
                Dim objScheduleItem As ScheduleItem
                objScheduleItem = CType(arrSource(i), ScheduleItem)
                arrDest.Add(objScheduleItem)
            Next
            Return arrDest
        End Function


        Public Function GetSchedule(ByVal TypeFullName As String, ByVal Server As String) As ScheduleItem
            Dim objScheduleItem As ScheduleItem = CType(CBO.FillObject(DataProvider.Instance.GetSchedule(TypeFullName, Server), GetType(ScheduleItem)), ScheduleItem)
            Return objScheduleItem
        End Function

        Public Function GetSchedule(ByVal ScheduleID As Integer) As ScheduleItem
            Dim objScheduleItem As ScheduleItem = CType(CBO.FillObject(DataProvider.Instance.GetSchedule(ScheduleID), GetType(ScheduleItem)), ScheduleItem)
            Return objScheduleItem
        End Function

        Public Function GetNextScheduledTask(ByVal Server As String) As ScheduleItem

            Dim objScheduleItem As ScheduleItem = CType(CBO.FillObject(DataProvider.Instance.GetNextScheduledTask(Server), GetType(ScheduleItem)), ScheduleItem)
            Return objScheduleItem

        End Function

        Public Function GetScheduleByEvent(ByVal EventName As String, ByVal Server As String) As ArrayList
            Dim arrSource As ArrayList = CBO.FillCollection(DataProvider.Instance.GetScheduleByEvent(EventName, Server), GetType(ScheduleHistoryItem))
            Dim arrDest As New ArrayList
            Dim i As Integer
            For i = 0 To arrSource.Count - 1
                Dim objScheduleItem As ScheduleItem
                objScheduleItem = CType(arrSource(i), ScheduleItem)
                arrDest.Add(objScheduleItem)
            Next
            Return arrDest
        End Function

        Public Function GetScheduleHistory(ByVal ScheduleID As Integer) As ArrayList
            Return CBO.FillCollection(DataProvider.Instance.GetScheduleHistory(ScheduleID), GetType(ScheduleHistoryItem))
        End Function

        Public Function GetScheduleQueue() As Collection
            Return CoreScheduler.GetScheduleQueue
        End Function

        Public Function GetScheduleProcessing() As Collection
            Return CoreScheduler.GetScheduleInProgress
        End Function

        Public Function GetScheduleStatus() As ScheduleStatus
            Return CoreScheduler.GetScheduleStatus
        End Function

        Public Function GetFreeThreadCount() As Integer
            Return CoreScheduler.GetFreeThreadCount()
        End Function

        Public Function GetActiveThreadCount() As Integer
            Return CoreScheduler.GetActiveThreadCount()
        End Function

        Public Function GetMaxThreadCount() As Integer
            Return CoreScheduler.GetMaxThreadCount
        End Function

        Public Sub ReloadSchedule()
            CoreScheduler.ReloadSchedule()
        End Sub

        Public Function AddSchedule(ByVal TypeFullName As String, ByVal TimeLapse As Integer, ByVal TimeLapseMeasurement As String, ByVal RetryTimeLapse As Integer, ByVal RetryTimeLapseMeasurement As String, ByVal RetainHistoryNum As Integer, ByVal AttachToEvent As String, ByVal CatchUpEnabled As Boolean, ByVal Enabled As Boolean, ByVal ObjectDependencies As String, ByVal Servers As String) As Integer
            Return DataProvider.Instance.AddSchedule(TypeFullName, TimeLapse, TimeLapseMeasurement, RetryTimeLapse, RetryTimeLapseMeasurement, RetainHistoryNum, AttachToEvent, CatchUpEnabled, Enabled, ObjectDependencies, Servers)
        End Function

        Public Sub UpdateSchedule(ByVal ScheduleID As Integer, ByVal TypeFullName As String, ByVal TimeLapse As Integer, ByVal TimeLapseMeasurement As String, ByVal RetryTimeLapse As Integer, ByVal RetryTimeLapseMeasurement As String, ByVal RetainHistoryNum As Integer, ByVal AttachToEvent As String, ByVal CatchUpEnabled As Boolean, ByVal Enabled As Boolean, ByVal ObjectDependencies As String, ByVal Servers As String)
            DataProvider.Instance.UpdateSchedule(ScheduleID, TypeFullName, TimeLapse, TimeLapseMeasurement, RetryTimeLapse, RetryTimeLapseMeasurement, RetainHistoryNum, AttachToEvent, CatchUpEnabled, Enabled, ObjectDependencies, Servers)
        End Sub

        Public Sub DeleteSchedule(ByVal ScheduleID As Integer)
            DataProvider.Instance.DeleteSchedule(ScheduleID)
        End Sub

        Public Function AddScheduleHistory(ByVal objScheduleHistoryItem As ScheduleHistoryItem) As Integer
            Return DataProvider.Instance.AddScheduleHistory(objScheduleHistoryItem.ScheduleID, objScheduleHistoryItem.StartDate, Common.Globals.ServerName)
        End Function

        Public Sub UpdateScheduleHistory(ByVal objScheduleHistoryItem As ScheduleHistoryItem)
            DataProvider.Instance.UpdateScheduleHistory(objScheduleHistoryItem.ScheduleHistoryID, objScheduleHistoryItem.EndDate, objScheduleHistoryItem.Succeeded, objScheduleHistoryItem.LogNotes, objScheduleHistoryItem.NextStart)
        End Sub

        Public Function GetScheduleItemSettings(ByVal ScheduleID As Integer) As Hashtable
            Dim h As New Hashtable
            Dim r As IDataReader = DataProvider.Instance.GetScheduleItemSettings(ScheduleID)
            While r.Read
                h.Add(r("SettingName"), r("SettingValue"))
            End While

            ' close datareader
            If Not r Is Nothing Then
                r.Close()
            End If

            Return h
        End Function

        Public Sub AddScheduleItemSetting(ByVal ScheduleID As Integer, ByVal Name As String, ByVal Value As String)
            DataProvider.Instance.AddScheduleItemSetting(ScheduleID, Name, Value)
        End Sub

        Public Sub PurgeScheduleHistory()
            DataProvider.Instance.PurgeScheduleHistory()
        End Sub

    End Class

End Namespace