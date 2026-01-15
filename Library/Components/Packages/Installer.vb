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

Imports ICSharpCode.SharpZipLib.Zip
Imports System.IO
Imports System.Text

Namespace DotNetNuke.Services.Packages

    ''' -----------------------------------------------------------------------------
    ''' <summary>
    ''' The Installer class provides a single entrypoint for Package Installation
    ''' </summary>
    ''' <history>
    ''' 	[cnurse]	07/24/2007  created
    ''' </history>
    ''' -----------------------------------------------------------------------------
    Public Class Installer

        Private _InstallerInfo As InstallerInfo

#Region "Constructors"

        ''' -----------------------------------------------------------------------------
        ''' <summary>
        ''' This Constructor creates a new Installer instance from a string representing
        ''' the physical path to the temporary install folder and a string representing 
        ''' the physical path to the root of the site
        ''' </summary>
        ''' <param name="tempFolder">The physical path to the zip file containg the package</param>
        ''' <param name="manifest">The manifest filename</param>
        ''' <param name="physicalSitePath">The physical path to the root of the site</param>
        ''' <history>
        ''' 	[cnurse]	07/24/2007  created
        ''' </history>
        ''' -----------------------------------------------------------------------------
        Public Sub New(ByVal tempFolder As String, ByVal manifest As String, ByVal physicalSitePath As String)
            _InstallerInfo = New InstallerInfo(tempFolder, manifest, physicalSitePath)
        End Sub

        ''' -----------------------------------------------------------------------------
        ''' <summary>
        ''' This Constructor creates a new Installer instance from a Stream and a
        ''' string representing the physical path to the root of the site
        ''' </summary>
        ''' <param name="inputStream">The Stream to use to create this InstallerInfo instance</param>
        ''' <param name="physicalSitePath">The physical path to the root of the site</param>
        ''' <history>
        ''' 	[cnurse]	07/24/2007  created
        ''' </history>
        ''' -----------------------------------------------------------------------------
        Public Sub New(ByVal inputStream As Stream, ByVal physicalSitePath As String)
            _InstallerInfo = New InstallerInfo(inputStream, physicalSitePath)
        End Sub

        ''' -----------------------------------------------------------------------------
        ''' <summary>
        ''' This Constructor creates a new Installer instance from a PackageInfo object
        ''' </summary>
        ''' <param name="package">The PackageInfo instance</param>
        ''' <history>
        ''' 	[cnurse]	07/31/2007  created
        ''' </history>
        ''' -----------------------------------------------------------------------------
        Public Sub New(ByVal package As PackageInfo, ByVal physicalSitePath As String)
            _InstallerInfo = New InstallerInfo(package, physicalSitePath)
        End Sub

#End Region

#Region "Public Properties"

        Public ReadOnly Property InstallerInfo() As InstallerInfo
            Get
                Return _InstallerInfo
            End Get
        End Property

        Public ReadOnly Property IsValid() As Boolean
            Get
                Return InstallerInfo.IsValid
            End Get
        End Property

        Public ReadOnly Property TempInstallFolder() As String
            Get
                Return InstallerInfo.TempInstallFolder
            End Get
        End Property

#End Region

#Region "Private Methods"

        Private Sub LogInstallEvent(ByVal package As String)
            Try
                Dim objEventLogInfo As New Services.Log.EventLog.LogInfo
                objEventLogInfo.LogTypeKey = Services.Log.EventLog.EventLogController.EventLogType.HOST_ALERT.ToString
                objEventLogInfo.LogProperties.Add(New DotNetNuke.Services.Log.EventLog.LogDetailInfo("Install " + package + ":", InstallerInfo.ManifestFile.Name.Replace(".dnn", "")))
                Dim objLogEntry As LogEntry
                For Each objLogEntry In InstallerInfo.Log.Logs
                    objEventLogInfo.LogProperties.Add(New DotNetNuke.Services.Log.EventLog.LogDetailInfo("Info:", objLogEntry.Description))
                Next
                Dim objEventLog As New Services.Log.EventLog.EventLogController
                objEventLog.AddLog(objEventLogInfo)
            Catch ex As Exception
                ' error
            End Try

        End Sub

#End Region

#Region "Public Methods"

        Public Function Install() As Boolean

            InstallerInfo.Log.StartJob(Util.INSTALL_Start)
            Try
                InstallerInfo.Install()
            Catch ex As Exception
                InstallerInfo.Log.AddFailure(ex)
                Return False
            End Try

            If InstallerInfo.Log.Valid Then
                InstallerInfo.Log.EndJob(Util.INSTALL_Success)
            Else
                InstallerInfo.Log.EndJob(Util.INSTALL_Failed)
            End If

            ' log installation event
            LogInstallEvent("Package")

            Return True

        End Function

        Public Function UnInstall() As Boolean

            InstallerInfo.Log.StartJob(Util.UNINSTALL_Start)
            Try
                InstallerInfo.UnInstall()
            Catch ex As Exception
                InstallerInfo.Log.AddFailure(ex)
                Return False
            End Try

            InstallerInfo.Log.EndJob(Util.UNINSTALL_Success)

            ' log installation event
            LogInstallEvent("Package")

            Return True

        End Function

#End Region

    End Class

End Namespace
