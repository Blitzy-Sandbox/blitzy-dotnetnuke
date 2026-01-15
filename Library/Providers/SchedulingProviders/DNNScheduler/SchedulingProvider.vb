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
Imports System.IO
Imports System.Web
Imports System.Xml
Imports System.Reflection
Imports System.Threading
Imports DotNetNuke.Services.Scheduling


Namespace DotNetNuke.Services.Scheduling.DNNScheduling

    Public Class DNNScheduler
        Inherits Services.Scheduling.SchedulingProvider

        Private Const ProviderType As String = "schedulingprovider"

        Public Overrides Function GetProviderPath() As String
            Return ProviderPath
        End Function

        Public Overrides Sub Start()
            If Enabled Then
                Dim s As New CoreScheduler(Debug, MaxThreads)
                CoreScheduler.KeepRunning = True
                CoreScheduler.KeepThreadAlive = True
                CoreScheduler.Start()
            End If
        End Sub

        Public Overrides Sub ExecuteTasks()
            If Enabled Then
                Dim s As New CoreScheduler(Debug, MaxThreads)
                CoreScheduler.KeepRunning = True
                CoreScheduler.KeepThreadAlive = False
                CoreScheduler.Start()
            End If
        End Sub

        Public Overrides Sub ReStart(ByVal SourceOfRestart As String)
            Halt(SourceOfRestart)
            StartAndWaitForResponse()
        End Sub

        Public Overrides Sub StartAndWaitForResponse()
            If Enabled Then
                Dim newThread As New Threading.Thread(AddressOf Start)
                newThread.IsBackground = True
                newThread.Start()

                'wait for up to 30 seconds for thread
                'to start up
                Dim i As Integer
                For i = 0 To 30
                    If GetScheduleStatus() <> ScheduleStatus.STOPPED Then Exit Sub
                    Thread.Sleep(1000)
                Next
            End If
        End Sub

        Public Overrides Sub Halt(ByVal SourceOfHalt As String)
            Dim s As New CoreScheduler(Debug, MaxThreads)
            CoreScheduler.Halt(SourceOfHalt)
            CoreScheduler.KeepRunning = False
        End Sub

        Public Overrides Sub PurgeScheduleHistory()
            Dim s As New CoreScheduler(MaxThreads)
            CoreScheduler.PurgeScheduleHistory()
        End Sub

        Public Overrides Sub RunEventSchedule(ByVal objEventName As Scheduling.EventName)
            If Enabled Then
                Dim s As New CoreScheduler(Debug, MaxThreads)
                CoreScheduler.RunEventSchedule(objEventName)
            End If
        End Sub

        Public Overloads Overrides Function GetSchedule() As ArrayList
            Dim s As New SchedulingController
            Return s.GetSchedule()
        End Function
        Public Overloads Overrides Function GetSchedule(ByVal Server As String) As ArrayList
            Dim s As New SchedulingController
            Return s.GetSchedule(Server)
        End Function

        Public Overloads Overrides Function GetSchedule(ByVal ScheduleID As Integer) As ScheduleItem
            Dim s As New SchedulingController
            Return s.GetSchedule(ScheduleID)
        End Function

        Public Overloads Overrides Function GetSchedule(ByVal TypeFullName As String, ByVal Server As String) As ScheduleItem
            Dim s As New SchedulingController
            Return s.GetSchedule(TypeFullName, Server)
        End Function

        Public Overrides Function GetNextScheduledTask(ByVal Server As String) As ScheduleItem
            Dim s As New SchedulingController
            Return s.GetNextScheduledTask(Server)
        End Function


        Public Overrides Function GetScheduleHistory(ByVal ScheduleID As Integer) As ArrayList
            Dim s As New SchedulingController
            Return s.GetScheduleHistory(ScheduleID)
        End Function

        Public Overrides Function GetScheduleQueue() As Collection
            Dim s As New SchedulingController
            Return s.GetScheduleQueue()
        End Function

        Public Overrides Function GetScheduleProcessing() As Collection
            Dim s As New SchedulingController
            Return s.GetScheduleProcessing()
        End Function

        Public Overrides Function GetScheduleStatus() As ScheduleStatus
            Dim s As New SchedulingController
            Return s.GetScheduleStatus
        End Function

        Public Overrides Function AddSchedule(ByVal objScheduleItem As ScheduleItem) As Integer
            CoreScheduler.RemoveFromScheduleQueue(objScheduleItem)

            Dim s As New SchedulingController
            Dim i As Integer = s.AddSchedule(objScheduleItem.TypeFullName, objScheduleItem.TimeLapse, objScheduleItem.TimeLapseMeasurement, objScheduleItem.RetryTimeLapse, objScheduleItem.RetryTimeLapseMeasurement, objScheduleItem.RetainHistoryNum, objScheduleItem.AttachToEvent, objScheduleItem.CatchUpEnabled, objScheduleItem.Enabled, objScheduleItem.ObjectDependencies, objScheduleItem.Servers)

            Dim objScheduleHistoryItem As New ScheduleHistoryItem(objScheduleItem)
            objScheduleHistoryItem.NextStart = Now
            objScheduleHistoryItem.ScheduleID = i

            If Not objScheduleHistoryItem.TimeLapse = DotNetNuke.Common.Utilities.Null.NullInteger _
             AndAlso Not objScheduleHistoryItem.TimeLapseMeasurement = DotNetNuke.Common.Utilities.Null.NullString _
             AndAlso objScheduleHistoryItem.Enabled Then
                objScheduleHistoryItem.ScheduleSource = ScheduleSource.STARTED_FROM_SCHEDULE_CHANGE
                CoreScheduler.AddToScheduleQueue(objScheduleHistoryItem)
            End If
            DotNetNuke.Common.Utilities.DataCache.RemoveCache("ScheduleLastPolled")
            Return i

        End Function

        Public Overrides Sub UpdateSchedule(ByVal objScheduleItem As ScheduleItem)
            CoreScheduler.RemoveFromScheduleQueue(objScheduleItem)

            Dim s As New SchedulingController
            s.UpdateSchedule(objScheduleItem.ScheduleID, objScheduleItem.TypeFullName, objScheduleItem.TimeLapse, objScheduleItem.TimeLapseMeasurement, objScheduleItem.RetryTimeLapse, objScheduleItem.RetryTimeLapseMeasurement, objScheduleItem.RetainHistoryNum, objScheduleItem.AttachToEvent, objScheduleItem.CatchUpEnabled, objScheduleItem.Enabled, objScheduleItem.ObjectDependencies, objScheduleItem.Servers)

            Dim objScheduleHistoryItem As New ScheduleHistoryItem(objScheduleItem)

            If Not objScheduleHistoryItem.TimeLapse = DotNetNuke.Common.Utilities.Null.NullInteger _
             AndAlso Not objScheduleHistoryItem.TimeLapseMeasurement = DotNetNuke.Common.Utilities.Null.NullString _
             AndAlso objScheduleHistoryItem.Enabled Then
                objScheduleHistoryItem.ScheduleSource = ScheduleSource.STARTED_FROM_SCHEDULE_CHANGE
                CoreScheduler.AddToScheduleQueue(objScheduleHistoryItem)
            End If
            DotNetNuke.Common.Utilities.DataCache.RemoveCache("ScheduleLastPolled")
        End Sub

        Public Overrides Sub DeleteSchedule(ByVal objScheduleItem As ScheduleItem)
            Dim s As New SchedulingController
            s.DeleteSchedule(objScheduleItem.ScheduleID)
            CoreScheduler.RemoveFromScheduleQueue(objScheduleItem)
            DotNetNuke.Common.Utilities.DataCache.RemoveCache("ScheduleLastPolled")
        End Sub

        Public Overrides Function GetScheduleItemSettings(ByVal ScheduleID As Integer) As Hashtable
            Dim s As New SchedulingController
            Return s.GetScheduleItemSettings(ScheduleID)
        End Function

        Public Overrides Sub AddScheduleItemSetting(ByVal ScheduleID As Integer, ByVal Name As String, ByVal Value As String)
            Dim s As New SchedulingController
            s.AddScheduleItemSetting(ScheduleID, Name, Value)
        End Sub

        Public Overrides Function GetFreeThreadCount() As Integer
            Dim s As New SchedulingController
            Return s.GetFreeThreadCount
        End Function

        Public Overrides Function GetActiveThreadCount() As Integer
            Dim s As New SchedulingController
            Return s.GetActiveThreadCount
        End Function

        Public Overrides Function GetMaxThreadCount() As Integer
            Dim s As New SchedulingController
            Return s.GetMaxThreadCount
        End Function

    End Class


End Namespace
