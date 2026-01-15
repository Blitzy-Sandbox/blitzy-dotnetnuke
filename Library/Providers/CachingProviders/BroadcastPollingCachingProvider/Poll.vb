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
Imports System.Collections
Imports Microsoft.VisualBasic
Imports System.Text
Imports System.Reflection
Imports DotNetNuke.Services.Cache
Imports DotNetNuke.Services.Exceptions
Imports DotNetNuke.Common
Imports DotNetNuke.Common.Utilities


Namespace DotNetNuke.Services.Cache.BroadcastPollingCachingProvider

    Public Class Poll
        Inherits DotNetNuke.Services.Scheduling.SchedulerClient

        Public Sub New(ByVal objScheduleHistoryItem As DotNetNuke.Services.Scheduling.ScheduleHistoryItem)
            MyBase.new()
            Me.ScheduleHistoryItem = objScheduleHistoryItem    'REQUIRED
        End Sub
        Public Overrides Sub DoWork()
            Try
                Dim c As New Controller
                Dim arr As ArrayList = c.GetBroadcasts(Common.Globals.ServerName)
                Dim i As Integer
                For i = 0 To arr.Count - 1
                    Dim objBroadcastInfo As BroadcastInfo
                    objBroadcastInfo = CType(arr(i), BroadcastInfo)
                    Select Case objBroadcastInfo.BroadcastType
                        Case "RemoveCachedItem"
                            BPCachingProvider.Instance.Remove(objBroadcastInfo.BroadcastMessage)
                    End Select
                    Me.ScheduleHistoryItem.AddLogNote("Broadcast Type: " + objBroadcastInfo.BroadcastType + ", Broadcast Message: " + objBroadcastInfo.BroadcastMessage & Chr(13))
                Next
                Me.ScheduleHistoryItem.Succeeded = True    'REQUIRED
            Catch exc As Exception    'REQUIRED

                Me.ScheduleHistoryItem.Succeeded = False    'REQUIRED

                Me.ScheduleHistoryItem.AddLogNote(String.Format("An error ocurred polling for server broadcasts.", exc.ToString))    'OPTIONAL

                'notification that we have errored
                Me.Errored(exc)    'REQUIRED

                'log the exception
                LogException(exc)    'OPTIONAL
            End Try
        End Sub

    End Class


End Namespace
