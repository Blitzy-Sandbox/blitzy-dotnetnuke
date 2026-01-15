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
Imports System.XML
Imports System.XML.Xsl
Imports System.Xml.Serialization
Imports System.Reflection
Imports System.Security.Permissions
Imports System.Threading
Imports DotNetNuke.Common.Utilities
Imports DotNetNuke.Framework.Providers
Imports DotNetNuke.Common
Imports DotNetNuke.Services.Exceptions

Namespace DotNetNuke.Services.Log.EventLog

    Public Class XMLLoggingProvider
        Inherits LoggingProvider
        Private Const ProviderType As String = "logging"
        Private _providerConfiguration As ProviderConfiguration = ProviderConfiguration.GetProviderConfiguration(ProviderType)

        Private Shared lockLog As New ReaderWriterLock
        Private Shared lockNotif As New ReaderWriterLock
        Private Shared lockPurgeLog As New ReaderWriterLock
        Private Const ReaderLockTimeout As Integer = 10000    'milliseconds
        Private Const WriterLockTimeout As Integer = 10000    'milliseconds

        Private Const PendingNotificationsFile As String = "PendingLogNotifications.xml.resources"
        Private Shared xmlConfigDoc As XmlDocument
        Public Shared LogQueue As New Collection

        Public Sub New()
            If xmlConfigDoc Is Nothing Then
                xmlConfigDoc = GetConfigDoc()
            End If
        End Sub

#Region "Abstract Method Implementation"

        '--------------------------------------------------------------
        'Method to add a log entry
        '--------------------------------------------------------------
        Public Overrides Sub AddLog(ByVal objLogInfo As LogInfo)
            Dim ConfigPortalID As String
            If objLogInfo.LogPortalID <> Null.NullInteger Then
                ConfigPortalID = objLogInfo.LogPortalID.ToString
            Else
                ConfigPortalID = "*"
            End If

            Dim objLogTypeConfigInfo As LogTypeConfigInfo
            objLogTypeConfigInfo = GetLogTypeConfig(ConfigPortalID, objLogInfo.LogTypeKey)
            If Not objLogTypeConfigInfo Is Nothing AndAlso objLogTypeConfigInfo.LoggingIsActive Then
                objLogInfo.LogFileID = objLogTypeConfigInfo.ID
                objLogInfo.LogCreateDateNum = DateToNum(objLogInfo.LogCreateDate)
                Dim objLogQueueItem As New LogQueueItem
                objLogQueueItem.LogString = objLogInfo.Serialize()
                objLogQueueItem.LogTypeConfigInfo = objLogTypeConfigInfo

                Dim UseEventLogBuffer As Boolean = True
                If Common.Globals.HostSettings.ContainsKey("EventLogBuffer") Then
                    If Convert.ToString(HostSettings("EventLogBuffer")) <> "Y" Then
                        UseEventLogBuffer = False
                    End If
                Else
                    UseEventLogBuffer = False
                End If

                Dim scheduler As Scheduling.SchedulingProvider = Scheduling.SchedulingProvider.Instance()
                If objLogInfo.BypassBuffering Or Scheduling.SchedulingProvider.Enabled = False Or scheduler.GetScheduleStatus = Scheduling.ScheduleStatus.STOPPED Or UseEventLogBuffer = False Then
                    WriteLog(objLogQueueItem)
                Else
                    LogQueue.Add(objLogQueueItem)
                End If
            End If
        End Sub

        '--------------------------------------------------------------
        'Method to add Log Type Configuration
        '--------------------------------------------------------------
        Public Overrides Sub AddLogTypeConfigInfo(ByVal ID As String, ByVal IsActive As Boolean, ByVal LogTypeKey As String, ByVal LogTypePortalID As String, ByVal KeepMostRecent As String, ByVal LogFileName As String, ByVal EmailNotificationIsActive As Boolean, ByVal NotificationThreshold As String, ByVal NotificationThresholdTime As String, ByVal NotificationThresholdTimeType As String, ByVal MailFromAddress As String, ByVal MailToAddress As String)
            Dim xmlDoc As XmlDocument = GetConfigDoc()
            Dim xmlNode As XmlNode
            xmlNode = xmlDoc.ImportNode(GetXMLFromLogTypeConfigInfo(ID, IsActive, LogTypeKey, LogTypePortalID, KeepMostRecent, LogFileName, EmailNotificationIsActive, NotificationThreshold, NotificationThresholdTime, NotificationThresholdTimeType, MailFromAddress, MailToAddress), True)
            xmlDoc.DocumentElement.AppendChild(xmlNode)
            xmlDoc.Save(GetConfigDocFileName)
            DataCache.RemoveCache("LoggingGetConfigDoc")
            xmlConfigDoc = GetConfigDoc()
        End Sub

        Public Overrides Sub AddLogType(ByVal LogTypeKey As String, ByVal LogTypeFriendlyName As String, ByVal LogTypeDescription As String, ByVal LogTypeCSSClass As String, ByVal LogTypeOwner As String)
            Dim xmlDoc As XmlDocument = GetConfigDoc()
            Dim xmlNode As XmlNode
            xmlNode = xmlDoc.ImportNode(GetXMLFromLogTypeInfo(LogTypeKey, LogTypeFriendlyName, LogTypeDescription, LogTypeCSSClass, LogTypeOwner), True)
            xmlDoc.DocumentElement.AppendChild(xmlNode)
            xmlDoc.Save(GetConfigDocFileName)
            DataCache.RemoveCache("LoggingGetConfigDoc")
            xmlConfigDoc = GetConfigDoc()
        End Sub

        Public Overrides Sub UpdateLogType(ByVal LogTypeKey As String, ByVal LogTypeFriendlyName As String, ByVal LogTypeDescription As String, ByVal LogTypeCSSClass As String, ByVal LogTypeOwner As String)
            DeleteLogType(LogTypeKey)
            AddLogType(LogTypeKey, LogTypeFriendlyName, LogTypeDescription, LogTypeCSSClass, LogTypeOwner)
        End Sub

        '--------------------------------------------------------------
        'Method to delete the log files
        '--------------------------------------------------------------
        Public Overrides Sub ClearLog()

            Dim xmlConfigDoc As XmlDocument = GetConfigDoc()


            Dim a As ArrayList
            a = GetLogFiles(xmlConfigDoc, "*", "*")

            Dim i As Integer
            For i = 0 To a.Count - 1
                File.Delete(Convert.ToString(a(i)))
            Next

        End Sub

        '--------------------------------------------------------------
        'Method to delete a log entry
        '--------------------------------------------------------------
        Public Overrides Sub DeleteLog(ByVal objLogInfo As LogInfo)

            Dim xmlConfigDoc As XmlDocument = GetConfigDoc()

            Dim strFileName As String = GetLogFileByLogFileID(GetConfigDoc, objLogInfo.LogFileID)

            Dim xmlDoc As New XmlDocument
            Dim intAttempts As Integer
            'wait for up to 100 milliseconds for the file
            'to be unlocked if it is not available
            Do Until xmlDoc.OuterXml <> "" Or intAttempts = 100
                intAttempts += 1
                Try
                    xmlDoc.Load(strFileName)
                Catch exc As IOException
                    Thread.Sleep(1)
                End Try
            Loop

            Dim xmlNode As XmlNode
            xmlNode = xmlDoc.SelectSingleNode("/logs/log[@LogGUID='" + objLogInfo.LogGUID + "']")
            If Not xmlNode Is Nothing Then
                xmlDoc.DocumentElement.RemoveChild(xmlNode)
            End If
            xmlDoc.Save(strFileName)
        End Sub

        '--------------------------------------------------------------
        'Method to delete Log Type Configuration
        '--------------------------------------------------------------
        Public Overrides Sub DeleteLogTypeConfigInfo(ByVal ID As String)
            Dim xmlDoc As XmlDocument = GetConfigDoc()
            Dim xmlNode As XmlNode = xmlDoc.DocumentElement.SelectSingleNode("LogTypeConfig[@LogFileID='" + ID + "']")
            If Not xmlNode Is Nothing Then
                xmlDoc.DocumentElement.RemoveChild(xmlNode)
                xmlDoc.Save(GetConfigDocFileName)
            End If
            DataCache.RemoveCache("LoggingGetConfigDoc")
            xmlConfigDoc = GetConfigDoc()
        End Sub

        Public Overrides Sub DeleteLogType(ByVal LogTypeKey As String)
            Dim xmlDoc As XmlDocument = GetConfigDoc()
            Dim xmlNode As XmlNode = xmlDoc.DocumentElement.SelectSingleNode("LogTypes/LogType[@LogTypeKey='" + LogTypeKey + "']")
            If Not xmlNode Is Nothing Then
                xmlDoc.DocumentElement.RemoveChild(xmlNode)
                xmlDoc.Save(GetConfigDocFileName)
            End If
            DataCache.RemoveCache("LoggingGetConfigDoc")
            xmlConfigDoc = GetConfigDoc()
        End Sub

        '--------------------------------------------------------------
        'Method to get all log entries for all portals & log types
        '--------------------------------------------------------------
        Public Overloads Overrides Function GetLog() As LogInfoArray
            Return GetLogFromXPath("logs/log", "*", "*", Integer.MaxValue, 1, 0)
        End Function

        '--------------------------------------------------------------
        'Method to get all log entries for specified portal & all log types
        '--------------------------------------------------------------
        Public Overloads Overrides Function GetLog(ByVal PortalID As Integer) As LogInfoArray
            If PortalID = -1 Then
                Return GetLog()
            Else
                Return GetLogFromXPath("logs/log[@LogPortalID='" + PortalID.ToString + "']", PortalID.ToString, "*", Integer.MaxValue, 1, 0)
            End If
        End Function

        '--------------------------------------------------------------
        'Method to get all log entries for all portals & specified log type
        '--------------------------------------------------------------
        Public Overloads Overrides Function GetLog(ByVal LogType As String) As LogInfoArray
            Return GetLogFromXPath("logs/log[@LogTypeKey='" + LogType + "']", "*", LogType, Integer.MaxValue, 1, 0)
        End Function

        '--------------------------------------------------------------
        'Method to get all log entries for specified portal & log type
        '--------------------------------------------------------------
        Public Overloads Overrides Function GetLog(ByVal PortalID As Integer, ByVal LogTypeKey As String) As LogInfoArray
            If PortalID = -1 Then
                Return GetLog(LogTypeKey, Integer.MaxValue, 1, 0)
            Else
                Return GetLogFromXPath("logs/log[@LogPortalID='" + PortalID.ToString + "' and @LogTypeKey='" + LogTypeKey + "']", PortalID.ToString, LogTypeKey, Integer.MaxValue, 1, 0)
            End If

        End Function

        Public Overloads Overrides Function GetLog(ByVal PageSize As Integer, ByVal PageIndex As Integer, ByRef TotalRecords As Integer) As LogInfoArray
            Return GetLogFromXPath("logs/log", "*", "*", PageSize, PageIndex, TotalRecords)
        End Function

        '--------------------------------------------------------------
        'Method to get all log entries for specified portal & all log types
        '--------------------------------------------------------------
        Public Overloads Overrides Function GetLog(ByVal PortalID As Integer, ByVal PageSize As Integer, ByVal PageIndex As Integer, ByRef TotalRecords As Integer) As LogInfoArray
            If PortalID = -1 Then
                Return GetLog()
            Else
                Return GetLogFromXPath("logs/log[@LogPortalID='" + PortalID.ToString + "']", PortalID.ToString, "*", PageSize, PageIndex, TotalRecords)
            End If
        End Function

        '--------------------------------------------------------------
        'Method to get all log entries for all portals & specified log type
        '--------------------------------------------------------------
        Public Overloads Overrides Function GetLog(ByVal LogType As String, ByVal PageSize As Integer, ByVal PageIndex As Integer, ByRef TotalRecords As Integer) As LogInfoArray
            Return GetLogFromXPath("logs/log[@LogTypeKey='" + LogType + "']", "*", LogType, PageSize, PageIndex, TotalRecords)
        End Function

        '--------------------------------------------------------------
        'Method to get all log entries for specified portal & log type
        '--------------------------------------------------------------
        Public Overloads Overrides Function GetLog(ByVal PortalID As Integer, ByVal LogTypeKey As String, ByVal PageSize As Integer, ByVal PageIndex As Integer, ByRef TotalRecords As Integer) As LogInfoArray
            If PortalID = -1 Then
                Return GetLog(LogTypeKey, PageSize, PageIndex, TotalRecords)
            Else
                Return GetLogFromXPath("logs/log[@LogPortalID='" + PortalID.ToString + "' and @LogTypeKey='" + LogTypeKey + "']", PortalID.ToString, LogTypeKey, PageSize, PageIndex, TotalRecords)
            End If

        End Function

        '--------------------------------------------------------------
        'Method to get Log Type Configuration
        '--------------------------------------------------------------
        Public Overrides Function GetLogTypeConfigInfo() As ArrayList
            Dim xmlConfigDoc As XmlDocument = GetConfigDoc()

            Dim xmlLogTypeConfigInfoList As XmlNodeList
            xmlLogTypeConfigInfoList = xmlConfigDoc.SelectNodes("/LogConfig/LogTypeConfig ")

            Dim arrLogTypeInfo As New ArrayList

            Dim xmlLogTypeConfigInfo As XmlNode
            For Each xmlLogTypeConfigInfo In xmlLogTypeConfigInfoList
                arrLogTypeInfo.Add(GetLogTypeConfigInfoFromXML(xmlLogTypeConfigInfo))
            Next
            Return arrLogTypeInfo
        End Function

        '--------------------------------------------------------------
        'Method to get Log Type Configuration by ID
        '--------------------------------------------------------------
        Public Overrides Function GetLogTypeConfigInfoByID(ByVal ID As String) As LogTypeConfigInfo
            Dim xmlConfigDoc As XmlDocument = GetConfigDoc()

            Dim xmlLogTypeConfigInfo As XmlNode
            xmlLogTypeConfigInfo = xmlConfigDoc.SelectSingleNode("/LogConfig/LogTypeConfig[@LogFileID='" + ID + "']")

            Return GetLogTypeConfigInfoFromXML(xmlLogTypeConfigInfo)

        End Function

        '--------------------------------------------------------------
        'Methods to get the log configuration info
        '--------------------------------------------------------------
        Public Overrides Function GetLogTypeInfo() As ArrayList
            Dim xmlConfigDoc As XmlDocument = GetConfigDoc()

            Dim xmlLogTypeInfoList As XmlNodeList
            xmlLogTypeInfoList = xmlConfigDoc.SelectNodes("/LogConfig/LogTypes/LogType")

            Dim arrLogTypeInfo As New ArrayList

            Dim xmlLogTypeInfo As XmlNode
            For Each xmlLogTypeInfo In xmlLogTypeInfoList
                arrLogTypeInfo.Add(GetLogTypeInfoFromXML(xmlLogTypeInfo))
            Next
            Return arrLogTypeInfo
        End Function

        Public Overrides Function GetSingleLog(ByVal objLogInfo As LogInfo, ByVal objReturnType As ReturnType) As Object
            Dim xmlConfigDoc As XmlDocument = GetConfigDoc()

            Dim strFileName As String = GetLogFileByLogFileID(GetConfigDoc, objLogInfo.LogFileID)

            Dim xmlDoc As New XmlDocument
            Dim intAttempts As Integer
            'wait for up to 100 milliseconds for the file
            'to be unlocked if it is not available
            Do Until xmlDoc.OuterXml <> "" Or intAttempts = 100
                intAttempts += 1
                Try
                    xmlDoc.Load(strFileName)
                Catch exc As IOException
                    Thread.Sleep(1)
                End Try
            Loop

            Dim objxmlNode As XmlNode
            objxmlNode = xmlDoc.SelectSingleNode("/logs/log[@LogGUID='" + objLogInfo.LogGUID + "']")

            Dim xmlDocOut As New XmlDocument
            xmlDocOut.LoadXml("<SingleLog></SingleLog>")
            Dim xmlNewNode As XmlNode
            xmlNewNode = xmlDocOut.ImportNode(objxmlNode, True)
            xmlDocOut.DocumentElement.AppendChild(xmlNewNode)

            If objReturnType = LoggingProvider.ReturnType.XML Then
                Return xmlDocOut.DocumentElement.SelectSingleNode("log")
            Else
                Return GetLogInfoFromXML(xmlDocOut, Int32.MaxValue, 1, 0).GetItem(0)
            End If

        End Function

        '--------------------------------------------------------------
        'Method to see if logging is enabled for a log type & portal
        '--------------------------------------------------------------
        Public Overrides Function LoggingIsEnabled(ByVal LogType As String, ByVal PortalID As Integer) As Boolean
            Dim objLogTypeConfigInfo As LogTypeConfigInfo
            Dim ConfigPortalID As String = PortalID.ToString
            If PortalID = -1 Then
                ConfigPortalID = "*"
            End If
            objLogTypeConfigInfo = GetLogTypeConfig(ConfigPortalID, LogType)
            If Not objLogTypeConfigInfo Is Nothing Then
                If objLogTypeConfigInfo.LoggingIsActive Then
                    Return True
                End If
            End If
            Return False
        End Function

        Public Overrides Sub PurgeLogBuffer()

            Try
                lockPurgeLog.AcquireWriterLock(WriterLockTimeout)
                Dim i As Integer
                Dim c As Collection = XMLLoggingProvider.LogQueue
                Dim j As Integer = c.Count
                For i = 1 To c.Count
                    'in case the log was removed
                    'by another thread simultaneously
                    If Not c(j) Is Nothing Then
                        Dim objLogQueueItem As LogQueueItem
                        objLogQueueItem = CType(c(j), LogQueueItem)
                        WriteLog(objLogQueueItem)
                    End If
                    'in case the log was removed
                    'by another thread simultaneously
                    If Not c(j) Is Nothing Then
                        c.Remove(j)
                    End If

                    'use "j" instead of "i" so we
                    'can iterate in reverse, taking
                    'items out of the rear of the
                    'collection
                    j -= 1
                Next


                Dim objMe As New XMLLoggingProvider
                Dim a As ArrayList = objMe.GetLogTypeConfigInfo()
                Dim k As Integer
                For k = 0 To a.Count - 1
                    Dim objLogTypeConfigInfo As LogTypeConfigInfo
                    objLogTypeConfigInfo = CType(a(k), LogTypeConfigInfo)
                    '--------------------------------------------------------------
                    'if the KeepMostRecent setting has a numeric value,
                    'use it to limit the log file to "x" of the most recent
                    'logs, where "x" is the value of KeepMostRecent.
                    'A value of "*" signifies to keep all log entries.
                    '--------------------------------------------------------------
                    If objLogTypeConfigInfo.KeepMostRecent <> "*" AndAlso objLogTypeConfigInfo.LogFileName <> "" Then
                        Dim xmlLog As New XmlDocument
                        Dim FileExists As Boolean
                        Try
                            Dim intAttempts As Integer
                            'wait for up to 100 milliseconds for the file
                            'to be unlocked if it is not available
                            Do Until xmlLog.OuterXml <> "" Or intAttempts = 100
                                intAttempts += 1
                                Try
                                    xmlLog.Load(objLogTypeConfigInfo.LogFileNameWithPath)
                                    FileExists = True
                                Catch exc As IOException
                                    Thread.Sleep(1)
                                End Try
                            Loop
                        Catch exc As FileNotFoundException
                            FileExists = False
                            'file doesn't exist
                        Catch exc As XmlException
                            FileExists = False
                            'file is corrupt
                        End Try
                        If FileExists Then
                            Dim objTotalNodes As XmlNodeList
                            If objLogTypeConfigInfo.LogTypePortalID = "*" Then
                                objTotalNodes = xmlLog.DocumentElement.SelectNodes("log[@LogTypeKey='" + objLogTypeConfigInfo.LogTypeKey + "']")
                            Else
                                objTotalNodes = xmlLog.DocumentElement.SelectNodes("log[@LogTypeKey='" + objLogTypeConfigInfo.LogTypeKey + "' and LogPortalID='" + objLogTypeConfigInfo.LogTypePortalID + "']")
                            End If

                            Dim intNodeCount As Integer = objTotalNodes.Count
                            Dim intKeepMostRecent As Integer = Convert.ToInt32(objLogTypeConfigInfo.KeepMostRecent)
                            If intNodeCount > intKeepMostRecent Then
                                Dim objTotalNode As XmlNode
                                Dim m As Integer = 0

                                For Each objTotalNode In objTotalNodes
                                    If intNodeCount - m > intKeepMostRecent Then
                                        xmlLog.DocumentElement.RemoveChild(objTotalNode)
                                    End If
                                    m += 1
                                Next
                                xmlLog.Save(objLogTypeConfigInfo.LogFileNameWithPath)
                            Else
                                xmlLog = Nothing
                            End If
                        End If
                    End If
                Next
            Finally
                lockPurgeLog.ReleaseWriterLock()
            End Try

        End Sub

        '--------------------------------------------------------------
        'Method to send email notifications
        '--------------------------------------------------------------
        Public Overrides Sub SendLogNotifications()

            Try

                lockNotif.AcquireWriterLock(WriterLockTimeout)
                Dim xmlPendingNotificationsDoc As New XmlDocument
                Try
                    xmlPendingNotificationsDoc.Load(GetFilePath(PendingNotificationsFile))
                Catch exc As FileNotFoundException
                    'file not found
                    Exit Sub
                End Try

                Dim arrLogTypeInfo As ArrayList
                Dim x As New XMLLoggingProvider
                arrLogTypeInfo = x.GetLogTypeConfigInfo

                PurgeLogBuffer()

                Dim a As Integer
                For a = 0 To arrLogTypeInfo.Count - 1
                    Dim objLogTypeInfo As LogTypeConfigInfo
                    objLogTypeInfo = CType(arrLogTypeInfo(a), LogTypeConfigInfo)

                    If objLogTypeInfo.EmailNotificationIsActive Then

                        Dim xmlPendingNotifications As XmlNodeList = xmlPendingNotificationsDoc.DocumentElement.SelectNodes _
                         ("log[@NotificationLogTypeKey='" + objLogTypeInfo.LogTypeKey + "' and @LogTypePortalID='" _
                         + objLogTypeInfo.LogTypePortalID + "' and number(@LogCreateDateNum) > " + DateToNum(objLogTypeInfo.StartDateTime).ToString + "]")

                        If xmlPendingNotifications.Count >= objLogTypeInfo.NotificationThreshold Then
                            'we have notifications to send out
                            Dim xmlPendingNotification As XmlNode
                            Dim xmlOut As New XmlDocument
                            xmlOut.LoadXml("<notification></notification>")
                            For Each xmlPendingNotification In xmlPendingNotifications

                                Dim tmpNode As XmlNode
                                tmpNode = xmlOut.ImportNode(xmlPendingNotification, True)
                                xmlOut.DocumentElement.AppendChild(tmpNode)

                                'Remove the node from the list of pending notifications
                                xmlPendingNotificationsDoc.DocumentElement.RemoveChild(xmlPendingNotification)
                            Next

                            Dim NotificationFailed As Boolean = False
                            Dim errSendNotif As String

                            errSendNotif = Mail.Mail.SendMail(objLogTypeInfo.MailFromAddress, objLogTypeInfo.MailToAddress, "", _
                                "Log Notification", xmlOut.OuterXml, "", "", "", "", "", "")

                            If errSendNotif <> "" Then
                                'notification failed to send
                                NotificationFailed = True
                            End If


                            Dim objEventLogController As New EventLogController
                            If NotificationFailed Then
                                'Notification failed, log it
                                Dim objEventLogInfo As New LogInfo
                                objEventLogInfo.LogTypeKey = Services.Log.EventLog.EventLogController.EventLogType.LOG_NOTIFICATION_FAILURE.ToString
                                objEventLogInfo.AddProperty("Log Notification Failed: ", errSendNotif)
                                objEventLogController.AddLog(objEventLogInfo)

                                'need to reload the xml doc because
                                'we removed xml nodes above
                                xmlPendingNotificationsDoc.Load(GetFilePath(PendingNotificationsFile))

                                If xmlPendingNotificationsDoc.DocumentElement.Attributes("LastNotificationFailure") Is Nothing Then
                                    Dim xmlNotificationFailed As XmlAttribute
                                    xmlNotificationFailed = xmlPendingNotificationsDoc.CreateAttribute("LastNotificationFailure")
                                    xmlNotificationFailed.Value = Date.Now.ToString
                                    xmlPendingNotificationsDoc.DocumentElement.Attributes.Append(xmlNotificationFailed)
                                Else
                                    xmlPendingNotificationsDoc.DocumentElement.Attributes("LastNotificationFailure").Value = Date.Now.ToString
                                End If
                                xmlPendingNotificationsDoc.Save(GetFilePath(PendingNotificationsFile))
                            Else
                                'Notification succeeded.
                                'Save the updated pending notifications file
                                'so we remove the notifications that have been completed.
                                If Not xmlPendingNotificationsDoc.DocumentElement.Attributes("LastNotificationFailure") Is Nothing Then
                                    xmlPendingNotificationsDoc.DocumentElement.Attributes.Remove(xmlPendingNotificationsDoc.DocumentElement.Attributes("LastNotificationFailure"))
                                End If

                                If xmlPendingNotificationsDoc.DocumentElement.Attributes("LastNotificationSuccess") Is Nothing Then
                                    Dim xmlNotificationSucceeded As XmlAttribute
                                    xmlNotificationSucceeded = xmlPendingNotificationsDoc.CreateAttribute("LastNotificationSuccess")
                                    xmlNotificationSucceeded.Value = Date.Now.ToString
                                    xmlPendingNotificationsDoc.DocumentElement.Attributes.Append(xmlNotificationSucceeded)
                                Else
                                    xmlPendingNotificationsDoc.DocumentElement.Attributes("LastNotificationSuccess").Value = Date.Now.ToString
                                End If
                                xmlPendingNotificationsDoc.Save(GetFilePath(PendingNotificationsFile))
                            End If



                        End If

                    End If

                Next

                x.DeleteOldPendingNotifications()

            Catch exc As Exception
                LogException(exc)
            Finally
                lockNotif.ReleaseWriterLock()
            End Try
        End Sub

        '--------------------------------------------------------------
        'Methods to return functionality support indicators
        '--------------------------------------------------------------
        Public Overrides Function SupportsEmailNotification() As Boolean
            Return True
        End Function

        Public Overrides Function SupportsInternalViewer() As Boolean
            Return True
        End Function

        Public Overrides Function SupportsSendToCoreTeam() As Boolean
            Return True
        End Function

        Public Overrides Function SupportsSendViaEmail() As Boolean
            Return True
        End Function

        '--------------------------------------------------------------
        'Method to update Log Type Configuration
        '--------------------------------------------------------------
        Public Overrides Sub UpdateLogTypeConfigInfo(ByVal ID As String, ByVal IsActive As Boolean, ByVal LogTypeKey As String, ByVal LogTypePortalID As String, ByVal KeepMostRecent As String, ByVal LogFileName As String, ByVal EmailNotificationIsActive As Boolean, ByVal NotificationThreshold As String, ByVal NotificationThresholdTime As String, ByVal NotificationThresholdTimeType As String, ByVal MailFromAddress As String, ByVal MailToAddress As String)
            DeleteLogTypeConfigInfo(ID)
            AddLogTypeConfigInfo(ID, IsActive, LogTypeKey, LogTypePortalID, KeepMostRecent, LogFileName, EmailNotificationIsActive, NotificationThreshold, NotificationThresholdTime, NotificationThresholdTimeType, MailFromAddress, MailToAddress)
        End Sub

#End Region

#Region "Private Helper Methods"

        Private Function DateToNum(ByVal dt As Date) As Long

            Dim i As Long
            i = Convert.ToInt64(dt.Year) * 10000000000000
            i += Convert.ToInt64(dt.Month) * 100000000000
            i += Convert.ToInt64(dt.Day) * 1000000000
            i += Convert.ToInt64(dt.Hour) * 10000000
            i += Convert.ToInt64(dt.Minute) * 100000
            i += Convert.ToInt64(dt.Second) * 1000
            i += Convert.ToInt64(dt.Millisecond) * 1
            Return i
        End Function

        Private Sub DeleteOldPendingNotifications()
            Dim xmlPendingNotificationsDoc As New XmlDocument
            Try
                xmlPendingNotificationsDoc.Load(GetFilePath(PendingNotificationsFile))
            Catch exc As FileNotFoundException
                'file not found
                Exit Sub
            End Try
            'Check to see if we have had any
            'errors sending notifications.
            'If so, get out of this sub
            'so we don't delete any pending
            'notifications.  We only want
            'to delete old notifications
            'if the last notification succeeded.
            Dim LastNotificationSuccess As Date = Null.NullDate
            Dim LastNotificationFailure As Date = Null.NullDate
            If Not xmlPendingNotificationsDoc.DocumentElement.Attributes("LastNotificationFailure") Is Nothing Then
                LastNotificationFailure = CType(xmlPendingNotificationsDoc.DocumentElement.Attributes("LastNotificationFailure").Value, Date)
            End If
            If Not xmlPendingNotificationsDoc.DocumentElement.Attributes("LastNotificationSuccess") Is Nothing Then
                LastNotificationSuccess = CType(xmlPendingNotificationsDoc.DocumentElement.Attributes("LastNotificationSuccess").Value, Date)
            End If

            If LastNotificationFailure > LastNotificationSuccess Then
                'the most recent notification cycle
                'failed, so we don't want to delete
                'any pending notifications
                Exit Sub
            End If

            Dim xmlConfigDoc As XmlDocument = GetConfigDoc()
            Dim arrLogTypeInfo As ArrayList
            arrLogTypeInfo = GetLogTypeConfigInfo()

            Dim a As Integer
            For a = 0 To arrLogTypeInfo.Count - 1
                Dim objLogTypeInfo As LogTypeConfigInfo
                objLogTypeInfo = CType(arrLogTypeInfo(a), LogTypeConfigInfo)

                If objLogTypeInfo.EmailNotificationIsActive Then
                    Dim xmlPendingNotifications As XmlNodeList = xmlPendingNotificationsDoc.DocumentElement.SelectNodes _
                      ("log[@LogTypeKey='" + objLogTypeInfo.LogTypeKey + "' and @LogTypePortalID='" _
                      + objLogTypeInfo.LogTypePortalID + "' and @CreateDateNum < '" + DateToNum(objLogTypeInfo.StartDateTime).ToString + "']")

                    If xmlPendingNotifications.Count > 0 Then
                        'we have pending notifications to delete
                        'because time has elapsed putting
                        'them out of scope for the log type settings
                        Dim xmlPendingNotification As XmlNode
                        For Each xmlPendingNotification In xmlPendingNotifications
                            'Remove the node from the list of pending notifications
                            xmlPendingNotificationsDoc.DocumentElement.RemoveChild(xmlPendingNotification)
                        Next
                    End If
                End If
            Next
            xmlPendingNotificationsDoc.Save(GetFilePath(PendingNotificationsFile))
        End Sub

        Private Function GetConfigDoc() As XmlDocument
            Dim xmlConfigDoc As XmlDocument = CType(DataCache.GetCache("LoggingGetConfigDoc"), XmlDocument)
            If xmlConfigDoc Is Nothing Then
                Dim strConfigDoc As String = GetConfigDocFileName()
                xmlConfigDoc = New XmlDocument
                If File.Exists(strConfigDoc) = False Then
                    Dim TemplateLogConfig As String = Globals.HostMapPath + "Logs\LogConfig\LogConfigTemplate.xml.resources"
                    File.Copy(TemplateLogConfig, strConfigDoc)
                    File.SetAttributes(strConfigDoc, FileAttributes.Normal)
                End If

                Dim intAttempts As Integer
                'wait for up to 100 milliseconds for the file
                'to be unlocked if it is not available
                Do Until xmlConfigDoc.OuterXml <> "" Or intAttempts = 100
                    intAttempts += 1
                    Try
                        xmlConfigDoc.Load(strConfigDoc)
                    Catch exc As IOException
                        Thread.Sleep(1)
                    End Try
                Loop

                Dim filePath As String = GetConfigDocFileName()
                If File.Exists(filePath) Then
                    DataCache.SetCache("LoggingGetConfigDoc", xmlConfigDoc, New System.Web.Caching.CacheDependency(filePath))
                End If
            End If
            Return xmlConfigDoc
        End Function

        Private Function GetConfigDocFileName() As String
            '--------------------------------------------------------------
            ' Read the configuration specific information for this provider
            '--------------------------------------------------------------
            Dim objProvider As Provider = CType(_providerConfiguration.Providers(_providerConfiguration.DefaultProvider), Provider)
            Return GetFilePath(objProvider.Attributes("configfilename"), "LogConfig\")
        End Function

        Private Function GetConfigProviderPath() As String
            '--------------------------------------------------------------
            ' Read the configuration specific information for this provider
            '--------------------------------------------------------------
            Dim objProvider As Provider = CType(_providerConfiguration.Providers(_providerConfiguration.DefaultProvider), Provider)
            Return HttpContext.Current.Server.MapPath(objProvider.Attributes("providerPath"))
        End Function

        Private Shared Function GetFilePath(ByVal strFileName As String) As String
            Return GetFilePath(strFileName, "")
        End Function

        Private Shared Function GetFilePath(ByVal strFileName As String, ByVal strFolder As String) As String
            '--------------------------------------------------------------
            'check to see if they entered a filename or an absolute file path
            '--------------------------------------------------------------
            If strFileName.LastIndexOf("\") = -1 And strFileName.LastIndexOf("/") = -1 Then
                '--------------------------------------------------------------
                'Config settings specified a filename only, with no absolute path
                'Use the /Portals/_default/Logs directory to store the log file
                'This allows user to specify alternative location for
                'log files to be stored, othre than the DNN directory.
                '--------------------------------------------------------------
                Dim objHttpContext As HttpContext = HttpContext.Current
                If strFolder <> "" Then
                    strFileName = Globals.HostMapPath + "Logs\" + strFolder + strFileName
                Else
                    strFileName = Globals.HostMapPath + "Logs\" + strFileName
                End If

            End If
            Return strFileName
        End Function

        Private Function GetLogFileByLogFileID(ByVal xmlConfigDoc As XmlDocument, ByVal LogFileID As String) As String
            Dim xmlLogFile As XmlNode
            xmlLogFile = xmlConfigDoc.SelectSingleNode("/LogConfig/LogTypeConfig[@LogFileID='" + LogFileID + "']/@FileName")

            If Not xmlLogFile Is Nothing Then
                Return GetFilePath(xmlLogFile.InnerText)
            End If

            Return ""
        End Function

        Private Function GetLogFiles(ByVal xmlConfigDoc As XmlDocument) As ArrayList
            Return GetLogFiles(xmlConfigDoc, "*", "*")
        End Function

        Private Function GetLogFiles(ByVal xmlConfigDoc As XmlDocument, ByVal ConfigPortalID As String) As ArrayList
            Return GetLogFiles(xmlConfigDoc, ConfigPortalID, "*")
        End Function

        Private Function GetLogFiles(ByVal xmlConfigDoc As XmlDocument, ByVal ConfigPortalID As String, ByVal LogTypeKey As String) As ArrayList

            Dim ht As New Hashtable
            Dim arrFiles As New ArrayList
            '--------------------------------------------------------------
            'First see if there is a log file specified
            'for this log type and this PortalID
            '--------------------------------------------------------------
            Dim xmlLogFiles As XmlNodeList = Nothing

            If ConfigPortalID = "*" And LogTypeKey = "*" Then
                xmlLogFiles = xmlConfigDoc.SelectNodes("/LogConfig/LogTypeConfig/@FileName")
            ElseIf ConfigPortalID <> "*" And LogTypeKey = "*" Then
                xmlLogFiles = xmlConfigDoc.SelectNodes("/LogConfig/LogTypeConfig[@LogTypePortalID='" + ConfigPortalID + "']/@FileName")
            ElseIf ConfigPortalID = "*" And LogTypeKey <> "*" Then
                xmlLogFiles = xmlConfigDoc.SelectNodes("/LogConfig/LogTypeConfig[@LogTypeKey='" + LogTypeKey + "']/@FileName")
            ElseIf ConfigPortalID <> "*" And LogTypeKey <> "*" Then
                xmlLogFiles = xmlConfigDoc.SelectNodes("/LogConfig/LogTypeConfig[@LogTypePortalID='" + ConfigPortalID + "' and @LogTypeKey='" + LogTypeKey + "']/@FileName")
            End If

            If Not xmlLogFiles Is Nothing Then
                Dim xmlLogFile As XmlNode
                For Each xmlLogFile In xmlLogFiles
                    If xmlLogFile.InnerText <> "" AndAlso ht(xmlLogFile.InnerText) Is Nothing Then
                        'dedupe
                        arrFiles.Add(GetFilePath(xmlLogFile.InnerText))
                        ht.Add(xmlLogFile.InnerText, True)
                    End If
                Next
            End If
            'If arrFiles.Count > 0 Then
            '	Return arrFiles
            'End If

            '--------------------------------------------------------------
            'This is a catch all...it gets the default log file name.
            '--------------------------------------------------------------
            Dim xmlDefaultLogFile As XmlNode
            xmlDefaultLogFile = xmlConfigDoc.SelectSingleNode("/LogConfig/LogTypeConfig[@LogTypeKey='*' and @LogTypePortalID='*']/@FileName")
            If Not xmlDefaultLogFile Is Nothing AndAlso ht(xmlDefaultLogFile.InnerText) Is Nothing Then
                'dedupe
                arrFiles.Add(GetFilePath(xmlDefaultLogFile.InnerText))
                ht.Add(xmlDefaultLogFile.InnerText, True)
            End If
            Return arrFiles

        End Function

        Private Function GetLogFromXPath(ByVal xpath As String, ByVal PortalID As String, ByVal LogType As String, ByVal PageSize As Integer, ByVal PageIndex As Integer, ByRef TotalRecords As Integer) As LogInfoArray
            Dim xmlConfigDoc As XmlDocument = GetConfigDoc()
            Dim arrLogFiles As ArrayList = GetLogFiles(xmlConfigDoc, PortalID, LogType)

            Dim xmlLogFiles As New XmlDocument
            xmlLogFiles.LoadXml("<LogCollection></LogCollection>")

            Dim xmlLogFilesDocEl As XmlElement
            xmlLogFilesDocEl = xmlLogFiles.DocumentElement

            Dim arrLogInfo As New ArrayList

            Dim i As Integer
            For i = 0 To arrLogFiles.Count - 1
                Dim FileIsCorrupt As Boolean = False
                Dim FileExists As Boolean = True
                Dim LogFile As String
                LogFile = Convert.ToString(arrLogFiles(i))
                Dim xmlLogFile As New XmlDocument
                Try
                    lockLog.AcquireReaderLock(ReaderLockTimeout)
                    xmlLogFile.Load(LogFile)
                Catch exc As FileNotFoundException
                    FileExists = False
                    'file doesn't exist
                Catch exc As XmlException
                    FileExists = False
                    FileIsCorrupt = True
                    'file is corrupt
                Finally
                    lockLog.ReleaseReaderLock()
                End Try
                If FileIsCorrupt Then
                    Dim s As String = "A log file is corrupt '" + LogFile + "'."
                    If InStr(LogFile, "Exceptions.xml.resources") > 0 Then
                        s += "  This could be due to an older exception log file being written to by the new logging provider.  Try removing 'Exceptions.xml.resources' from the logs directory to solve the problem."
                    End If
                    Dim objEventLogInfo As New LogInfo
                    objEventLogInfo.AddProperty("Note", s)
                    objEventLogInfo.BypassBuffering = True
                    objEventLogInfo.LogTypeKey = "HOST_ALERT"
                    Dim objEventLog As New EventLogController
                    objEventLog.AddLog(objEventLogInfo)
                ElseIf FileExists Then
                    Dim xmlNodes As XmlNodeList
                    xmlNodes = xmlLogFile.SelectNodes(xpath)

                    Dim xmlLogNodes As XmlElement
                    xmlLogNodes = xmlLogFiles.CreateElement("logs")

                    Dim xmlNode As XmlNode
                    For Each xmlNode In xmlNodes
                        xmlLogNodes.AppendChild(xmlLogFiles.ImportNode(xmlNode, True))
                    Next

                    xmlLogFilesDocEl.AppendChild(xmlLogNodes)
                End If
            Next

            Return GetLogInfoFromXML(xmlLogFiles, PageSize, PageIndex, TotalRecords)
        End Function

        Private Function GetLogInfoFromXML(ByVal xmlLogFiles As XmlDocument, ByVal PageSize As Integer, ByVal PageIndex As Integer, ByRef TotalRecords As Integer) As LogInfoArray

            'Create the Stream to place the output.         
            Dim str As System.IO.Stream = New System.IO.MemoryStream
            Dim xw As System.IO.StreamWriter = New System.IO.StreamWriter(str, System.Text.Encoding.UTF8)

            'Transform the file.
            XmlUtils.XSLTransform(xmlLogFiles, xw, GetConfigProviderPath() + "log.xslt")

            'flush and set the position to 0
            xw.Flush()
            str.Position = 0

            Dim x As System.IO.StreamReader = New System.IO.StreamReader(str)
            xmlLogFiles.Load(x)
            x.Close()

            TotalRecords = xmlLogFiles.DocumentElement.SelectNodes("logs/log").Count
            Dim TotalPages As Integer
            TotalPages = Convert.ToInt32(Math.Ceiling(CType(TotalRecords / PageSize, Double)))
            Dim LowNum As Integer
            Dim HighNum As Integer
            LowNum = PageIndex * PageSize
            HighNum = (PageIndex * PageSize) + PageSize
            If HighNum > TotalRecords Then HighNum = TotalRecords

            Dim arrLog As New LogInfoArray

            For Each XmlNode As XmlNode In xmlLogFiles.DocumentElement.SelectNodes("logs/log[position()>=" + LowNum.ToString + " and position()<" + HighNum.ToString + "]")
                Dim Log As New LogInfo(XmlNode.OuterXml)
                arrLog.Add(Log)
            Next
            Return arrLog
        End Function

        Private Function GetLogTypeConfig(ByVal ConfigPortalID As String, ByVal LogTypeKey As String) As LogTypeConfigInfo

            '--------------------------------------------------------------
            'First see if there is a log file specified
            'for this log type and this PortalID
            '--------------------------------------------------------------
            Dim xmlLogTypeInfo As XmlNode = xmlConfigDoc.SelectSingleNode("/LogConfig/LogTypeConfig[@LogTypeKey='" + LogTypeKey + "' and @LogTypePortalID='" + ConfigPortalID + "']")
            If Not xmlLogTypeInfo Is Nothing Then
                Return GetLogTypeConfigInfoFromXML(xmlLogTypeInfo)
            End If

            '--------------------------------------------------------------
            'There's no log file specified for this
            'log type and PortalID, so check to see
            'if there is a log file specified for
            'all LogTypes for this PortalID
            '--------------------------------------------------------------
            xmlLogTypeInfo = xmlConfigDoc.SelectSingleNode("/LogConfig/LogTypeConfig[@LogTypeKey='*' and @LogTypePortalID='" + ConfigPortalID + "']")
            If Not xmlLogTypeInfo Is Nothing Then
                Return GetLogTypeConfigInfoFromXML(xmlLogTypeInfo)
            End If

            '--------------------------------------------------------------
            'No logfile has been found yet, so let's
            'check if there is a logfile specified
            'for this log type and all PortalIDs
            '--------------------------------------------------------------
            xmlLogTypeInfo = xmlConfigDoc.SelectSingleNode("/LogConfig/LogTypeConfig[@LogTypeKey='" + LogTypeKey + "' and @LogTypePortalID='*']")
            If Not xmlLogTypeInfo Is Nothing Then
                Return GetLogTypeConfigInfoFromXML(xmlLogTypeInfo)
            End If

            '--------------------------------------------------------------
            'This is a catch all...it gets the default log file name.
            '--------------------------------------------------------------
            xmlLogTypeInfo = xmlConfigDoc.SelectSingleNode("/LogConfig/LogTypeConfig[@LogTypeKey='*' and @LogTypePortalID='*']")
            If Not xmlLogTypeInfo Is Nothing Then
                Return GetLogTypeConfigInfoFromXML(xmlLogTypeInfo)
            End If

            Return Nothing
        End Function

        Private Function GetLogTypeConfigInfoFromXML(ByVal xmlLogTypeInfo As XmlNode) As LogTypeConfigInfo
            Dim objLogTypeConfigInfo As New LogTypeConfigInfo
            objLogTypeConfigInfo.LogFileNameWithPath = GetFilePath(xmlLogTypeInfo.Attributes("FileName").Value)
            objLogTypeConfigInfo.LogFileName = xmlLogTypeInfo.Attributes("FileName").Value
            objLogTypeConfigInfo.LogTypeKey = xmlLogTypeInfo.Attributes("LogTypeKey").Value
            If Not xmlLogTypeInfo.Attributes("LogTypePortalID") Is Nothing Then
                objLogTypeConfigInfo.LogTypePortalID = xmlLogTypeInfo.Attributes("LogTypePortalID").Value
            Else
                objLogTypeConfigInfo.LogTypePortalID = "*"
            End If
            If Not xmlLogTypeInfo.Attributes("KeepMostRecent") Is Nothing Then
                objLogTypeConfigInfo.KeepMostRecent = xmlLogTypeInfo.Attributes("KeepMostRecent").Value
            Else
                objLogTypeConfigInfo.KeepMostRecent = "*"
            End If
            objLogTypeConfigInfo.ID = xmlLogTypeInfo.Attributes("LogFileID").Value
            objLogTypeConfigInfo.LoggingIsActive = CType(IIf(LCase(xmlLogTypeInfo.Attributes("LoggingStatus").Value.ToString) = "on", True, False), Boolean)
            If Not xmlLogTypeInfo.Attributes("EmailNotificationStatus") Is Nothing Then
                objLogTypeConfigInfo.EmailNotificationIsActive = CType(IIf(LCase(xmlLogTypeInfo.Attributes("EmailNotificationStatus").Value.ToString) = "on", True, False), Boolean)
            Else
                objLogTypeConfigInfo.EmailNotificationIsActive = False
            End If
            If Not xmlLogTypeInfo.Attributes("MailFromAddress") Is Nothing Then
                objLogTypeConfigInfo.MailFromAddress = Convert.ToString(xmlLogTypeInfo.Attributes("MailFromAddress").Value)
            Else
                objLogTypeConfigInfo.MailFromAddress = Null.NullString
            End If
            If Not xmlLogTypeInfo.Attributes("MailToAddress") Is Nothing Then
                objLogTypeConfigInfo.MailToAddress = Convert.ToString(xmlLogTypeInfo.Attributes("MailToAddress").Value)
            Else
                objLogTypeConfigInfo.MailToAddress = Null.NullString
            End If
            If Not xmlLogTypeInfo.Attributes("NotificationThreshold") Is Nothing Then
                objLogTypeConfigInfo.NotificationThreshold = Convert.ToInt32(xmlLogTypeInfo.Attributes("NotificationThreshold").Value)
            Else
                objLogTypeConfigInfo.NotificationThreshold = -1
            End If
            If Not xmlLogTypeInfo.Attributes("NotificationThresholdTime") Is Nothing Then
                objLogTypeConfigInfo.NotificationThresholdTime = Convert.ToInt32(xmlLogTypeInfo.Attributes("NotificationThresholdTime").Value)
            Else
                objLogTypeConfigInfo.NotificationThresholdTime = -1
            End If
            If Not xmlLogTypeInfo.Attributes("NotificationThresholdTimeType") Is Nothing Then
                Dim NotificationThresholdTimeType As String = Convert.ToString(xmlLogTypeInfo.Attributes("NotificationThresholdTimeType").Value)
                Select Case NotificationThresholdTimeType
                    Case "1"
                        objLogTypeConfigInfo.NotificationThresholdTimeType = LogTypeConfigInfo.NotificationThresholdTimeTypes.Seconds
                    Case "2"
                        objLogTypeConfigInfo.NotificationThresholdTimeType = LogTypeConfigInfo.NotificationThresholdTimeTypes.Minutes
                    Case "3"
                        objLogTypeConfigInfo.NotificationThresholdTimeType = LogTypeConfigInfo.NotificationThresholdTimeTypes.Hours
                    Case "4"
                        objLogTypeConfigInfo.NotificationThresholdTimeType = LogTypeConfigInfo.NotificationThresholdTimeTypes.Days
                    Case Else
                        objLogTypeConfigInfo.NotificationThresholdTimeType = LogTypeConfigInfo.NotificationThresholdTimeTypes.None
                End Select
            Else
                objLogTypeConfigInfo.NotificationThresholdTimeType = LogTypeConfigInfo.NotificationThresholdTimeTypes.None
            End If

            Return objLogTypeConfigInfo
        End Function

        Private Function GetLogTypeInfoFromXML(ByVal xmlLogTypeInfo As XmlNode) As LogTypeInfo
            Dim objLogTypeInfo As New LogTypeInfo
            objLogTypeInfo.LogTypeKey = xmlLogTypeInfo.Attributes("LogTypeKey").Value
            objLogTypeInfo.LogTypeFriendlyName = xmlLogTypeInfo.Attributes("LogTypeFriendlyName").Value
            objLogTypeInfo.LogTypeDescription = xmlLogTypeInfo.Attributes("LogTypeDescription").Value
            objLogTypeInfo.LogTypeOwner = xmlLogTypeInfo.Attributes("LogTypeOwner").Value
            objLogTypeInfo.LogTypeCSSClass = xmlLogTypeInfo.Attributes("LogTypeCSSClass").Value
            Return objLogTypeInfo
        End Function

        Private Function GetXMLFromLogTypeConfigInfo(ByVal LogFileID As String, ByVal IsActive As Boolean, ByVal LogTypeKey As String, ByVal LogTypePortalID As String, ByVal KeepMostRecent As String, ByVal LogFileName As String, ByVal EmailNotificationIsActive As Boolean, ByVal NotificationThreshold As String, ByVal NotificationThresholdTime As String, ByVal NotificationThresholdTimeType As String, ByVal MailFromAddress As String, ByVal MailToAddress As String) As XmlElement
            Dim BlankLogConfig As String
            BlankLogConfig = "	<LogTypeConfig LogFileID="""" LoggingStatus="""" LogTypeKey="""" LogTypePortalID="""" KeepMostRecent="""" FileName="""" EmailNotificationStatus="""" NotificationThreshold="""" NotificationThresholdTime="""" NotificationThresholdTimeType="""" MailFromAddress="""" MailToAddress=""""/>"
            Dim xmlDoc As New XmlDocument
            xmlDoc.LoadXml(BlankLogConfig)
            Dim xmlDocEl As XmlElement
            xmlDocEl = xmlDoc.DocumentElement
            xmlDocEl.Attributes("LogFileID").Value = LogFileID
            xmlDocEl.Attributes("LoggingStatus").Value = Convert.ToString(IIf(IsActive = True, "On", "Off"))
            xmlDocEl.Attributes("LogTypeKey").Value = LogTypeKey
            xmlDocEl.Attributes("LogTypePortalID").Value = LogTypePortalID
            xmlDocEl.Attributes("KeepMostRecent").Value = KeepMostRecent
            xmlDocEl.Attributes("FileName").Value = LogFileName
            xmlDocEl.Attributes("EmailNotificationStatus").Value = Convert.ToString(IIf(EmailNotificationIsActive = True, "On", "Off"))
            xmlDocEl.Attributes("NotificationThreshold").Value = NotificationThreshold
            xmlDocEl.Attributes("NotificationThresholdTime").Value = NotificationThresholdTime
            xmlDocEl.Attributes("NotificationThresholdTimeType").Value = NotificationThresholdTimeType
            xmlDocEl.Attributes("MailFromAddress").Value = MailFromAddress
            xmlDocEl.Attributes("MailToAddress").Value = MailToAddress
            Return xmlDocEl
        End Function

        Private Function GetXMLFromLogTypeInfo(ByVal LogTypeKey As String, ByVal LogTypeFriendlyName As String, ByVal LogTypeDescription As String, ByVal LogTypeCSSClass As String, ByVal LogTypeOwner As String) As XmlElement
            Dim BlankLogConfig As String
            BlankLogConfig = "<LogType LogTypeKey="""" LogTypeFriendlyName="""" LogTypeDescription="""" LogTypeOwner="""" LogTypeCSSClass=""""/>"
            Dim xmlDoc As New XmlDocument
            xmlDoc.LoadXml(BlankLogConfig)
            Dim xmlDocEl As XmlElement
            xmlDocEl = xmlDoc.DocumentElement
            xmlDocEl.Attributes("LogTypeKey").Value = LogTypeKey
            xmlDocEl.Attributes("LogTypeFriendlyName").Value = LogTypeFriendlyName
            xmlDocEl.Attributes("LogTypeDescription").Value = LogTypeDescription
            xmlDocEl.Attributes("LogTypeOwner").Value = LogTypeOwner
            xmlDocEl.Attributes("LogTypeCSSClass").Value = LogTypeCSSClass
            Return xmlDocEl
        End Function

        Private Sub WriteLog(ByVal objLogQueueItem As LogQueueItem)

            '--------------------------------------------------------------
            'Write the log entry
            '--------------------------------------------------------------
            Dim fs As FileStream = Nothing
            Dim sw As StreamWriter = Nothing

            Dim objLogTypeConfigInfo As LogTypeConfigInfo = objLogQueueItem.LogTypeConfigInfo
            Dim LogString As String = objLogQueueItem.LogString

            Try
                If objLogTypeConfigInfo.LogFileNameWithPath <> "" Then
                    '--------------------------------------------------------------
                    ' Write the entry to the log. 
                    '--------------------------------------------------------------
                    lockLog.AcquireWriterLock(WriterLockTimeout)
                    Dim intAttempts As Integer
                    'wait for up to 100 milliseconds for the file
                    'to be unlocked if it is not available
                    Do Until Not fs Is Nothing Or intAttempts = 100
                        intAttempts += 1
                        Try
                            fs = New FileStream(objLogTypeConfigInfo.LogFileNameWithPath, FileMode.OpenOrCreate, FileAccess.Write, FileShare.None)
                        Catch exc As IOException
                            Thread.Sleep(1)
                        End Try
                    Loop

                    If fs Is Nothing Then
                        If Not HttpContext.Current Is Nothing Then
                            HttpContext.Current.Response.Write("An error has occurred writing to the exception log.")
                            HttpContext.Current.Response.End()
                        End If
                    Else
                        '--------------------------------------------------------------
                        'Instantiate a new StreamWriter
                        '--------------------------------------------------------------
                        sw = New StreamWriter(fs, System.Text.Encoding.UTF8)
                        Dim FileLength As Long
                        FileLength = fs.Length
                        '--------------------------------------------------------------
                        'check to see if this file is new
                        '--------------------------------------------------------------
                        If FileLength > 0 Then
                            '--------------------------------------------------------------
                            'file is not new, set the position to just before
                            'the closing root element tag
                            '--------------------------------------------------------------
                            fs.Position = FileLength - 9
                        Else
                            '--------------------------------------------------------------
                            'file is new, create the opening root element tag
                            '--------------------------------------------------------------
                            LogString = "<logs>" + LogString
                        End If

                        '--------------------------------------------------------------
                        'write out our exception
                        '--------------------------------------------------------------
                        sw.WriteLine(LogString + "</logs>")
                        sw.Flush()
                    End If
                    If Not sw Is Nothing Then
                        sw.Close()
                    End If
                    If Not fs Is Nothing Then
                        fs.Close()
                    End If
                End If
                If objLogTypeConfigInfo.EmailNotificationIsActive = True Then
                    Try
                        lockNotif.AcquireWriterLock(ReaderLockTimeout)

                        Dim xmlDoc As New XmlDocument
                        xmlDoc.LoadXml(LogString)

                        'If the threshold for email notifications is 
                        'set to 0, send an email notification each
                        'time a log entry is written.


                        If objLogTypeConfigInfo.NotificationThreshold = 0 Then
                            Mail.Mail.SendMail(objLogTypeConfigInfo.MailFromAddress, objLogTypeConfigInfo.MailToAddress, _
                                "", "Event Notification", xmlDoc.InnerXml, "", "", "", "", "", "")
                        ElseIf objLogTypeConfigInfo.LogTypeKey <> "LOG_NOTIFICATION_FAILURE" Then
                            Dim xmlPendingNotificationsDoc As New XmlDocument
                            Try
                                xmlPendingNotificationsDoc.Load(GetFilePath(PendingNotificationsFile))
                            Catch exc As FileNotFoundException
                                'file not created yet
                                xmlPendingNotificationsDoc.LoadXml("<PendingNotifications></PendingNotifications>")
                            End Try
                            Dim xmlLogNode As XmlNode
                            xmlLogNode = xmlPendingNotificationsDoc.ImportNode(xmlDoc.FirstChild, True)

                            Dim xmlAttrib As XmlAttribute
                            xmlAttrib = xmlPendingNotificationsDoc.CreateAttribute("MailFromAddress")
                            xmlAttrib.Value = Convert.ToString(objLogTypeConfigInfo.MailFromAddress)
                            xmlLogNode.Attributes.Append(xmlAttrib)


                            xmlAttrib = xmlPendingNotificationsDoc.CreateAttribute("NotificationLogTypeKey")
                            xmlAttrib.Value = Convert.ToString(objLogTypeConfigInfo.LogTypeKey)
                            xmlLogNode.Attributes.Append(xmlAttrib)

                            xmlAttrib = xmlPendingNotificationsDoc.CreateAttribute("LogTypePortalID")
                            If objLogTypeConfigInfo.LogTypePortalID = "-1" Then
                                xmlAttrib.Value = "*"
                            Else
                                xmlAttrib.Value = objLogTypeConfigInfo.LogTypePortalID
                            End If
                            xmlLogNode.Attributes.Append(xmlAttrib)

                            Dim x As XmlElement
                            x = xmlPendingNotificationsDoc.CreateElement("EmailAddress")
                            x.InnerText = Convert.ToString(objLogTypeConfigInfo.MailToAddress)
                            xmlLogNode.AppendChild(x)

                            xmlPendingNotificationsDoc.DocumentElement.AppendChild(xmlLogNode)
                            xmlPendingNotificationsDoc.Save(GetFilePath(PendingNotificationsFile))
                        End If
                    Finally
                        lockNotif.ReleaseWriterLock()
                    End Try
                End If
                '--------------------------------------------------------------
                'handle the more common exceptions up 
                'front, leave less common ones to the end
                '--------------------------------------------------------------
            Catch exc As UnauthorizedAccessException
                If Not HttpContext.Current Is Nothing Then
                    Dim response As HttpResponse = HttpContext.Current.Response
                    HtmlUtils.WriteHeader(response, "Unauthorized Access Error")

                    Dim strMessage As String = exc.Message & " The Windows User Account listed below must have Read/Write Privileges to this path."
                    HtmlUtils.WriteError(response, objLogTypeConfigInfo.LogFileNameWithPath, strMessage)

                    HtmlUtils.WriteFooter(response)
                    response.End()
                End If
            Catch exc As DirectoryNotFoundException
                If Not HttpContext.Current Is Nothing Then
                    Dim response As HttpResponse = HttpContext.Current.Response
                    HtmlUtils.WriteHeader(response, "Directory Not Found Error")

                    Dim strMessage As String = exc.Message
                    HtmlUtils.WriteError(response, objLogTypeConfigInfo.LogFileNameWithPath, strMessage)

                    HtmlUtils.WriteFooter(response)
                    response.End()
                End If
            Catch exc As PathTooLongException
                If Not HttpContext.Current Is Nothing Then
                    Dim response As HttpResponse = HttpContext.Current.Response
                    HtmlUtils.WriteHeader(response, "Path Too Long Error")

                    Dim strMessage As String = exc.Message
                    HtmlUtils.WriteError(response, objLogTypeConfigInfo.LogFileNameWithPath, strMessage)

                    HtmlUtils.WriteFooter(response)
                    response.End()
                End If
            Catch exc As IOException
                If Not HttpContext.Current Is Nothing Then
                    Dim response As HttpResponse = HttpContext.Current.Response
                    HtmlUtils.WriteHeader(response, "IO Error")

                    Dim strMessage As String = exc.Message
                    HtmlUtils.WriteError(response, objLogTypeConfigInfo.LogFileNameWithPath, strMessage)

                    HtmlUtils.WriteFooter(response)
                    response.End()
                End If
            Catch exc As Exception
                If Not HttpContext.Current Is Nothing Then
                    Dim response As HttpResponse = HttpContext.Current.Response
                    HtmlUtils.WriteHeader(response, "Unhandled Error")

                    Dim strMessage As String = exc.Message
                    HtmlUtils.WriteError(response, objLogTypeConfigInfo.LogFileNameWithPath, strMessage)

                    HtmlUtils.WriteFooter(response)
                    response.End()
                End If
            Finally
                If Not sw Is Nothing Then
                    sw.Close()
                End If
                If Not fs Is Nothing Then
                    fs.Close()
                End If

                lockLog.ReleaseWriterLock()

            End Try

        End Sub

#End Region

    End Class

    Public Class LogQueueItem
        Private _LogString As String
        Private _LogTypeConfigInfo As LogTypeConfigInfo

        Public Property LogString() As String
            Get
                Return _LogString
            End Get
            Set(ByVal Value As String)
                _LogString = Value
            End Set
        End Property

        Public Property LogTypeConfigInfo() As LogTypeConfigInfo
            Get
                Return _LogTypeConfigInfo
            End Get
            Set(ByVal Value As LogTypeConfigInfo)
                _LogTypeConfigInfo = Value
            End Set
        End Property

    End Class

End Namespace
