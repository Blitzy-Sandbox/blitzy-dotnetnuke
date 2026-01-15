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

Imports System.IO
Imports System.Xml
Imports DotNetNuke.Entities.Modules
Imports DotNetNuke.Entities.Modules.Definitions

Namespace DotNetNuke.Modules.Admin.ResourceInstaller

    ''' -----------------------------------------------------------------------------
    ''' <summary>
    ''' The PaDnnAdapter_V3 extends PaDnnAdapter_V2 to support V3 Modules
    ''' </summary>
    ''' <remarks>
    ''' </remarks>
    ''' <history>
    ''' 	[cnurse]	04/21/2005  documented
    ''' </history>
    ''' -----------------------------------------------------------------------------
    Public Class PaDnnAdapter_V3
        Inherits PaDnnAdapter_V2

#Region "Constructors"

        Public Sub New(ByVal InstallerInfo As PaInstallInfo)
            MyBase.New(InstallerInfo)
        End Sub

#End Region

#Region "Protected Methods"

        Protected Overrides Sub LogValidFormat()

            InstallerInfo.Log.AddInfo(String.Format(DNN_Valid, "3.0"))

        End Sub

        Protected Overrides Function GetModuleFromNode(ByVal TempModuleDefinitionID As Integer, ByVal Folder As PaFolder, ByVal DNNModule As System.Xml.XmlElement) As Entities.Modules.Definitions.ModuleDefinitionInfo
            Dim ModuleDef As ModuleDefinitionInfo = MyBase.GetModuleFromNode(TempModuleDefinitionID, Folder, DNNModule)

            If Not ModuleDef Is Nothing Then
                Dim cacheElement As XmlElement = DirectCast(DNNModule.SelectSingleNode("cachetime"), XmlElement)
                If Not cacheElement Is Nothing Then
                    ModuleDef.DefaultCacheTime = CInt(cacheElement.InnerText.Trim)
                End If
            End If

            Return ModuleDef
        End Function

        Protected Overrides Function GetModuleControlFromNode(ByVal Foldername As String, ByVal TempModuleID As Integer, ByVal ModuleControl As System.Xml.XmlElement) As Entities.Modules.ModuleControlInfo
            ' Version 3 .dnn file format adds the helpurl node to the controls/control node element
            Dim ModControl As Entities.Modules.ModuleControlInfo = MyBase.GetModuleControlFromNode(Foldername, TempModuleID, ModuleControl)
            If Not ModControl Is Nothing Then
                Dim helpElement As XmlElement = DirectCast(ModuleControl.SelectSingleNode("helpurl"), XmlElement)
                If Not helpElement Is Nothing Then
                    ModControl.HelpURL = helpElement.InnerText.Trim
                End If
                Dim renderingElement As XmlElement = DirectCast(ModuleControl.SelectSingleNode("supportspartialrendering"), XmlElement)
                If Not renderingElement Is Nothing Then
                    ModControl.SupportsPartialRendering = Boolean.Parse(renderingElement.InnerText.Trim)
                End If
            End If
            Return ModControl
        End Function

        Protected Overrides Function GetFolderAttributesFromNode(ByVal FolderElement As System.Xml.XmlElement) As PaFolder

            ' call the V2 implementation to load the values
            Dim folder As PaFolder = MyBase.GetFolderAttributesFromNode(FolderElement)

            ' V3 .dnn file format adds the optional businesscontrollerclass node to the folder node element
            Dim businessControllerElement As XmlElement = DirectCast(FolderElement.SelectSingleNode("businesscontrollerclass"), XmlElement)
            If Not businessControllerElement Is Nothing Then
                folder.BusinessControllerClass = businessControllerElement.InnerText.Trim
            End If

            ' V3 .dnn file format adds the optional friendlyname/foldername/modulename nodes to the folder node element
            'For these new nodes the defaults are as follows
            ' <friendlyname>, <name>
            ' <foldernamee>, <name>, "MyModule"
            ' <modulename>, <friendlyname>, <name>
            Dim friendlynameElement As XmlElement = DirectCast(FolderElement.SelectSingleNode("friendlyname"), XmlElement)
            If Not friendlynameElement Is Nothing Then
                folder.FriendlyName = friendlynameElement.InnerText.Trim
                folder.ModuleName = friendlynameElement.InnerText.Trim
            End If

            Dim foldernameElement As XmlElement = DirectCast(FolderElement.SelectSingleNode("foldername"), XmlElement)
            If Not foldernameElement Is Nothing Then
                folder.FolderName = foldernameElement.InnerText.Trim
            End If
            If folder.FolderName = "" Then folder.FolderName = "MyModule"

            Dim modulenameElement As XmlElement = DirectCast(FolderElement.SelectSingleNode("modulename"), XmlElement)
            If Not modulenameElement Is Nothing Then
                folder.ModuleName = modulenameElement.InnerText.Trim
            End If

            ' V4.3.6 .dnn file format adds the optional compatibleversions node to the folder node element
            Dim compatibleVersionsElement As XmlElement = DirectCast(FolderElement.SelectSingleNode("compatibleversions"), XmlElement)
            If Not compatibleVersionsElement Is Nothing Then
                folder.CompatibleVersions = compatibleVersionsElement.InnerText.Trim
            End If
            ' V4.4.0 .dnn file format adds the optional supportsprobingprivatepath node to the folder node element
            Dim supportsProbingPrivatePath As XmlElement = DirectCast(FolderElement.SelectSingleNode("supportsprobingprivatepath"), XmlElement)
            If Not supportsProbingPrivatePath Is Nothing Then
                folder.SupportsProbingPrivatePath = Convert.ToBoolean(supportsProbingPrivatePath.InnerText.Trim)
            End If
            ' V4.5.0 .dnn file format adds the optional dependencies node to the folder node element
            Dim dependenciesElement As XmlElement = DirectCast(FolderElement.SelectSingleNode("dependencies"), XmlElement)
            If Not dependenciesElement Is Nothing Then
                folder.Dependencies = dependenciesElement.InnerText.Trim
            End If
            ' V4.5.0 .dnn file format adds the optional permissions node to the folder node element
            Dim permissionsElement As XmlElement = DirectCast(FolderElement.SelectSingleNode("permissions"), XmlElement)
            If Not permissionsElement Is Nothing Then
                folder.Permissions = permissionsElement.InnerText.Trim
            End If

            Return folder

        End Function

#End Region

    End Class
End Namespace