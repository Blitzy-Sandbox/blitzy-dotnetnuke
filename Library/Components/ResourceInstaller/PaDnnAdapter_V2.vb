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
Imports DotNetNuke.Modules.Admin.ResourceInstaller

Namespace DotNetNuke.Modules.Admin.ResourceInstaller

    ''' -----------------------------------------------------------------------------
    ''' <summary>
    ''' The PaDnnAdapter_V2 extends PaDnnAdapterBase to support V2 Modules
    ''' </summary>
    ''' <remarks>
    ''' </remarks>
    ''' <history>
    ''' 	[cnurse]	04/21/2005  documented
    ''' </history>
    ''' -----------------------------------------------------------------------------
    Public Class PaDnnAdapter_V2
        Inherits PaDnnAdapterBase

#Region "Constructors"

        Public Sub New(ByVal InstallerInfo As PaInstallInfo)
            MyBase.New(InstallerInfo)
        End Sub

#End Region

#Region "Private Methods"

        Private Function ValidateDnn() As ArrayList
            'Create New Validator
            Dim ModuleValidator As New ModuleDefinitionValidator

            'Tell Validator what XML to use
            Dim xmlStream As New MemoryStream(InstallerInfo.DnnFile.Buffer)
            ModuleValidator.Validate(xmlStream)
            Return ModuleValidator.Errors
        End Function

#End Region

#Region "Protected Methods"

        Protected Overridable Function GetFolderFromNode(ByRef TempModuleDefinitionID As Integer, ByVal FolderElement As XmlElement) As PaFolder

            Dim Folder As PaFolder = GetFolderAttributesFromNode(FolderElement)

            Dim resourcefileElement As XmlElement = DirectCast(FolderElement.SelectSingleNode("resourcefile"), XmlElement)
            If Not resourcefileElement Is Nothing Then
                If resourcefileElement.InnerText.Trim <> "" Then
                    Folder.ResourceFile = resourcefileElement.InnerText.Trim
                    Dim paRF As PaFile = CType(InstallerInfo.FileTable(Folder.ResourceFile.ToLower), PaFile)
                    If paRF Is Nothing Then
                        InstallerInfo.Log.AddFailure(EXCEPTION_MissingResource + Folder.ResourceFile)
                    Else
                        'need to add this file to the list of files to be installed
                        Folder.Files.Add(paRF)
                    End If
                End If
            End If

            ' loading files
            InstallerInfo.Log.AddInfo(FILES_Loading)
            Dim file As XmlElement
            For Each file In FolderElement.SelectNodes("files/file")
                'The file/name node is a required node.  If empty, this will throw
                'an exception.
                Dim name As String
                Try
                    name = file.SelectSingleNode("name").InnerText.Trim
                Catch ex As Exception
                    Throw New Exception(EXCEPTION_FolderName)
                End Try
                Dim paf As PaFile = CType(InstallerInfo.FileTable(name.ToLower), PaFile)
                If paf Is Nothing Then
                    InstallerInfo.Log.AddFailure(FILE_NotFound + name)
                Else
                    'A file path may or may not exist
                    Dim pathElement As XmlElement = DirectCast(file.SelectSingleNode("path"), XmlElement)
                    If Not (pathElement Is Nothing) Then
                        Dim filepath As String = pathElement.InnerText.Trim
                        InstallerInfo.Log.AddInfo(String.Format(FILE_Found, filepath, name))
                        paf.Path = filepath
                    End If
                    Folder.Files.Add(paf)
                End If
            Next file

            ' add dnn file to collection ( if it does not exist already )
            If Folder.Files.Contains(InstallerInfo.DnnFile) = False Then
                Folder.Files.Add(InstallerInfo.DnnFile)
            End If

            InstallerInfo.Log.AddInfo(MODULES_Loading)
            Dim DNNModule As XmlElement
            For Each DNNModule In FolderElement.SelectNodes("modules/module")

                Folder.Modules.Add(GetModuleFromNode(TempModuleDefinitionID, Folder, DNNModule))

                TempModuleDefinitionID += 1

            Next DNNModule

            Return Folder

        End Function

        Protected Overridable Function GetFolderAttributesFromNode(ByVal FolderElement As XmlElement) As PaFolder
            Dim Folder As New PaFolder

            'The folder/name node is a required node.  If empty, this will throw an exception.
            Try
                Folder.Name = FolderElement.SelectSingleNode("name").InnerText.Trim
            Catch ex As Exception
                Throw New Exception(EXCEPTION_FolderName)
            End Try

            Dim descriptionElement As XmlElement = DirectCast(FolderElement.SelectSingleNode("description"), XmlElement)
            If Not descriptionElement Is Nothing Then
                Folder.Description = descriptionElement.InnerText.Trim
            End If

            'The folder/version node is a required node.  If empty, this will throw an exception.
            Try
                Folder.Version = FolderElement.SelectSingleNode("version").InnerText.Trim
            Catch ex As Exception
                Throw New Exception(EXCEPTION_FolderVersion)
            End Try

            ' in V2 the FriendlyName/FolderName/ModuleName attributes were not supported in the *.dnn file,
            ' so set them to the Folder Name
            Folder.FriendlyName = Folder.Name
            Folder.FolderName = Folder.Name
            Folder.ModuleName = Folder.Name

            Return Folder

        End Function

        Protected Overridable Function GetModuleFromNode(ByVal TempModuleDefinitionID As Integer, ByVal Folder As PaFolder, ByVal DNNModule As XmlElement) As ModuleDefinitionInfo

            Dim ModuleDef As New ModuleDefinitionInfo

            Dim friendlyNameElement As XmlElement = DirectCast(DNNModule.SelectSingleNode("friendlyname"), XmlElement)
            If Not friendlyNameElement Is Nothing Then
                ModuleDef.FriendlyName = friendlyNameElement.InnerText.Trim
            End If

            ModuleDef.TempModuleID = TempModuleDefinitionID
            'We need to ensure that admin order is null for "User" modules

            InstallerInfo.Log.AddInfo(String.Format(MODULES_ControlInfo, ModuleDef.FriendlyName))

            Dim ModuleControl As XmlElement
            For Each ModuleControl In DNNModule.SelectNodes("controls/control")

                Folder.Controls.Add(GetModuleControlFromNode(Folder.Name, ModuleDef.TempModuleID, ModuleControl))

            Next ModuleControl

            Return ModuleDef

        End Function

        Protected Overridable Function GetModuleControlFromNode(ByVal Foldername As String, ByVal TempModuleID As Integer, ByVal ModuleControl As XmlElement) As ModuleControlInfo

            Dim ModuleControlDef As New ModuleControlInfo

            Dim keyElement As XmlElement = DirectCast(ModuleControl.SelectSingleNode("key"), XmlElement)
            If Not keyElement Is Nothing Then
                ModuleControlDef.ControlKey = keyElement.InnerText.Trim
            End If

            Dim titleElement As XmlElement = DirectCast(ModuleControl.SelectSingleNode("title"), XmlElement)
            If Not titleElement Is Nothing Then
                ModuleControlDef.ControlTitle = titleElement.InnerText.Trim
            End If

            Try
                Dim ControlSrc As String = ModuleControl.SelectSingleNode("src").InnerText.Trim
                If ControlSrc.ToLower.StartsWith("desktopmodules") OrElse Not ControlSrc.ToLower.EndsWith(".ascx") Then
                    ' this code allows a developer to reference an ASCX file in a different folder than the module folder ( good for ASCX files shared between modules where you want only a single copy )
                    'or it allows thedeveloper to use webcontrols rather than usercontrols
                    ModuleControlDef.ControlSrc = ControlSrc
                Else
                    ModuleControlDef.ControlSrc = Path.Combine("DesktopModules", Path.Combine(Foldername, ControlSrc))
                End If
                ModuleControlDef.ControlSrc = ModuleControlDef.ControlSrc.Replace("\"c, "/")
            Catch ex As Exception
                Throw New Exception(EXCEPTION_Src)
            End Try

            Dim iconFileElement As XmlElement = DirectCast(ModuleControl.SelectSingleNode("iconfile"), XmlElement)
            If Not iconFileElement Is Nothing Then
                ModuleControlDef.IconFile = iconFileElement.InnerText.Trim
            End If

            Try
                ModuleControlDef.ControlType = CType(TypeDescriptor.GetConverter(GetType(SecurityAccessLevel)).ConvertFromString(ModuleControl.SelectSingleNode("type").InnerText.Trim), SecurityAccessLevel)
            Catch ex As Exception
                Throw New Exception(EXCEPTION_Type)
            End Try

            Dim orderElement As XmlElement = DirectCast(ModuleControl.SelectSingleNode("vieworder"), XmlElement)
            If Not orderElement Is Nothing Then
                ModuleControlDef.ViewOrder = CType(orderElement.InnerText.Trim, Integer)
            Else
                ModuleControlDef.ViewOrder = Null.NullInteger
            End If

            'This is a temporary relationship since the ModuleDef has not been saved to the db
            'it does not have a valid ModuleDefID.  Once it is saved then we can update the
            'ModuleControlDef with the correct value.
            ModuleControlDef.ModuleDefID = TempModuleID

            Return ModuleControlDef
        End Function

        Protected Overridable Sub LogValidFormat()

            InstallerInfo.Log.AddInfo(String.Format(DNN_Valid, "2.0"))

        End Sub

#End Region

#Region "Public Methods"

        Public Overrides Function ReadDnn() As PaFolderCollection

            'This is a very long subroutine and should probably be broken down
            'into a couple of smaller routines.  That would make it easier to 
            'maintain.
            InstallerInfo.Log.StartJob(DNN_Reading)

            LogValidFormat()

            Dim Folders As New PaFolderCollection

            Dim doc As New XmlDocument
            Dim buffer As New MemoryStream(InstallerInfo.DnnFile.Buffer, False)
            doc.Load(buffer)
            InstallerInfo.Log.AddInfo(XML_Loaded)

            Dim dnnRoot As XmlNode = doc.DocumentElement

            Dim TempModuleDefinitionID As Integer = 0

            Dim FolderElement As XmlElement
            For Each FolderElement In dnnRoot.SelectNodes("folders/folder")
                'We will process each folder individually.  So we don't need to keep
                'as much in memory.

                Folders.Add(GetFolderFromNode(TempModuleDefinitionID, FolderElement))

            Next FolderElement

            If Not InstallerInfo.Log.Valid Then
                Throw New Exception(EXCEPTION_LoadFailed)
            End If
            InstallerInfo.Log.EndJob(DNN_Success)

            Return Folders
        End Function

#End Region

    End Class
End Namespace
