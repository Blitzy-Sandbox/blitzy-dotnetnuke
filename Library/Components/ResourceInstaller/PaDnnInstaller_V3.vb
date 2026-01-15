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
Imports DotNetNuke.Entities.Modules
Imports DotNetNuke.Services.EventQueue

Namespace DotNetNuke.Modules.Admin.ResourceInstaller

    ''' -----------------------------------------------------------------------------
    ''' <summary>
    ''' The PaDnnInstaller_V3 extends PaDnnInstallerBase to support V3 Modules
    ''' </summary>
    ''' <remarks>
    ''' </remarks>
    ''' <history>
    ''' 	[cnurse]	04/21/2005  documented
    ''' </history>
    ''' -----------------------------------------------------------------------------
    Public Class PaDnnInstaller_V3
        Inherits PaDnnInstallerBase

#Region "Constructors"

        Public Sub New(ByVal InstallerInfo As PaInstallInfo)
            MyBase.New(InstallerInfo)
        End Sub

#End Region

#Region "Protected Methods"

        Protected Overrides Function GetDesktopModule(ByVal Folder As PaFolder) As DesktopModuleInfo

            Dim objDesktopModules As New DesktopModuleController
            Dim objDesktopModule As DesktopModuleInfo = objDesktopModules.GetDesktopModuleByModuleName(Folder.ModuleName)

            Return objDesktopModule

        End Function

        Protected Overrides Function GetDesktopModuleSettings(ByVal objDesktopModule As DesktopModuleInfo, ByVal Folder As PaFolder) As Entities.Modules.DesktopModuleInfo

            ' call the V2 implementation to load the values
            objDesktopModule = MyBase.GetDesktopModuleSettings(objDesktopModule, Folder)

            ' V3 .dnn file format adds the optional businesscontrollerclass node to the folder node element
            objDesktopModule.BusinessControllerClass = Folder.BusinessControllerClass

            ' V4.3.6 .dnn file format adds the optional compatibleversions node to the folder node element
            objDesktopModule.CompatibleVersions = Folder.CompatibleVersions

            ' V4.5.0 .dnn file format adds the optional dependencies and permissions node to the folder node element
            objDesktopModule.Dependencies = Folder.Dependencies
            objDesktopModule.Permissions = Folder.Permissions

            Return objDesktopModule
        End Function

        Protected Overrides Function UpgradeModule(ByVal ModuleInfo As Entities.Modules.DesktopModuleInfo) As String
            Dim UpgradeVersionsList As String = ""

            If UpgradeVersions.Count > 0 Then
                For Each Version As String In UpgradeVersions
                    UpgradeVersionsList = UpgradeVersionsList & Version & ","
                    DeleteFiles(ModuleInfo.FolderName, Version)
                Next
                If UpgradeVersionsList.EndsWith(",") Then
                    UpgradeVersionsList = UpgradeVersionsList.Remove(UpgradeVersionsList.Length - 1, 1)
                End If
            Else
                UpgradeVersionsList = ModuleInfo.Version
            End If

            If ModuleInfo.BusinessControllerClass <> "" Then
                'the UpgradeModule interface method cannot be called at this time because 
                'the module may not be loaded into the app domain yet
                'So send an EventMessage that will process the update 
                'after the App recycles
                Dim oAppStartMessage As New EventQueue.EventMessage
                oAppStartMessage.Priority = MessagePriority.High
                oAppStartMessage.ExpirationDate = Now.AddYears(-1)
                oAppStartMessage.SentDate = System.DateTime.Now
                oAppStartMessage.Body = ""
                oAppStartMessage.ProcessorType = "DotNetNuke.Entities.Modules.EventMessageProcessor, DotNetNuke"
                oAppStartMessage.ProcessorCommand = "UpgradeModule"

                oAppStartMessage.Attributes.Add("BusinessControllerClass", ModuleInfo.BusinessControllerClass)
                oAppStartMessage.Attributes.Add("DesktopModuleId", ModuleInfo.DesktopModuleID.ToString())
                oAppStartMessage.Attributes.Add("UpgradeVersionsList", UpgradeVersionsList)

                'send it to occur on next App_Start Event
                EventQueueController.SendMessage(oAppStartMessage, "Application_Start")
            Else
                Dim oDesktopModuleController As New DesktopModuleController
                Dim oDesktopModule As DesktopModuleInfo = oDesktopModuleController.GetDesktopModule(ModuleInfo.DesktopModuleID)

                If (oDesktopModule IsNot Nothing) Then
                    'Initialise the SupportedFeatures
                    oDesktopModule.SupportedFeatures = 0
					oDesktopModuleController.UpdateDesktopModule(oDesktopModule, False)
                End If
            End If

            'TODO: Need to implement a feedback loop to display the results of the upgrade.
            Return ""
        End Function

        Protected Function DeleteFiles(ByVal FolderName As String, ByVal Version As String) As Boolean

            Dim WasSuccessful As Boolean = True

            Dim strListFile As String = Path.Combine(Path.Combine(InstallerInfo.SitePath, Path.Combine("DesktopModules", FolderName)), Version & ".txt")

            If File.Exists(strListFile) Then
                ' read list file
                Dim objStreamReader As StreamReader
                objStreamReader = File.OpenText(strListFile)
                Dim arrPaths As Array = objStreamReader.ReadToEnd.Split(ControlChars.CrLf.ToCharArray())
                objStreamReader.Close()
                FileSystemUtils.DeleteFiles(arrPaths)
            End If

            Return WasSuccessful

        End Function

#End Region

    End Class
End Namespace
