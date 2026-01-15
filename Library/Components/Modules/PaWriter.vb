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
Imports ICSharpCode.SharpZipLib.Checksums
Imports ICSharpCode.SharpZipLib.GZip
Imports System.IO
Imports System.Xml
Imports System.Xml.Serialization
Imports DotNetNuke.Modules.Admin.ResourceInstaller
Imports DotNetNuke.Entities.Modules.Definitions
Imports DotNetNuke.Services.Localization
Imports DotNetNuke.Framework.Providers

Namespace DotNetNuke.Entities.Modules

    ''' -----------------------------------------------------------------------------
    ''' Project  : DotNetNuke
    ''' Class  : PaWriter
    ''' -----------------------------------------------------------------------------
    ''' <summary>
    ''' The PaWriter class packages a Module as a Private Assembly.
    ''' </summary>
    ''' <remarks>
    ''' </remarks>
    ''' <history>
    '''  [cnurse] 01/13/2005 created
    ''' </history>
    ''' -----------------------------------------------------------------------------
    Public Class PaWriter

#Region "Private Members"

        Private _CreateManifest As Boolean = True
        Private _IncludeSource As Boolean = False
        Private _ProgressLog As New PaLogger
        Private _ResourceFileName As String
        Private _SupportsProbingPrivatePath As Boolean = False
        Private _ZipFile As String

        'Source Folder of PA
        Private _Folder As String
        Private _AppCodeFolder As String

        'Name of PA
        Private _Name As String

        'List of Files to include in PA
        Private _Files As New ArrayList


#End Region

#Region "Constructors"

        Public Sub New()
            Me.New(False, "")
        End Sub

        Public Sub New(ByVal IncludeSource As Boolean, ByVal ZipFile As String)
            _IncludeSource = IncludeSource
            _ZipFile = ZipFile
        End Sub

        Public Sub New(ByVal IncludeSource As Boolean, ByVal CreateManifest As Boolean, ByVal SupportsProbingPrivatePath As Boolean, ByVal ZipFile As String)
            _CreateManifest = CreateManifest
            _IncludeSource = IncludeSource
            _SupportsProbingPrivatePath = SupportsProbingPrivatePath
            _ZipFile = ZipFile
        End Sub

#End Region

#Region "Protected Properties"

        Protected ReadOnly Property AppCodeFolder() As String
            Get
                Return _AppCodeFolder
            End Get
        End Property

        Protected ReadOnly Property CreateManifest() As Boolean
            Get
                Return _CreateManifest
            End Get
        End Property

        Protected ReadOnly Property Folder() As String
            Get
                Return _Folder
            End Get
        End Property

        Protected ReadOnly Property IncludeSource() As Boolean
            Get
                Return _IncludeSource
            End Get
        End Property

        Protected ReadOnly Property ResourceFileName() As String
            Get
                Return _ResourceFileName
            End Get
        End Property

        Protected ReadOnly Property SupportsProbingPrivatePath() As Boolean
            Get
                Return _SupportsProbingPrivatePath
            End Get
        End Property


#End Region

#Region "Public Properties"

        Public ReadOnly Property ProgressLog() As PaLogger
            Get
                Return _ProgressLog
            End Get
        End Property

        Public Property ZipFile() As String
            Get
                Return _ZipFile
            End Get
            Set(ByVal Value As String)
                _ZipFile = Value
            End Set
        End Property

#End Region

#Region "Private Methods"

        Private Sub AddFile(ByVal File As PaFileInfo, ByVal AllowUnsafeExtensions As Boolean)
            Dim objPaFileInfo As PaFileInfo
            Dim blnAdd As Boolean = True
            For Each objPaFileInfo In _Files
                If objPaFileInfo.FullName = File.FullName Then
                    blnAdd = False
                    Exit For
                End If
            Next
            If Not AllowUnsafeExtensions Then
                If Right(File.FullName, 3).ToLower = "dnn" Or Right(File.FullName, 10).ToLower = "dnn.config" Then blnAdd = False
            End If

            If blnAdd Then
                _Files.Add(File)
            End If

        End Sub

        Private Sub AddFile(ByVal File As PaFileInfo)
            AddFile(File, False)
        End Sub

        Private Sub CreateDnnManifest(ByVal objDesktopModule As DesktopModuleInfo)

            Dim filename As String = ""
            _Name = objDesktopModule.ModuleName

            'Create Manifest Document
            Dim xmlManifest As New XmlDocument

            'Root Element
            Dim nodeRoot As XmlNode = xmlManifest.CreateElement("dotnetnuke")
            nodeRoot.Attributes.Append(XmlUtils.CreateAttribute(xmlManifest, "version", "3.0"))
            nodeRoot.Attributes.Append(XmlUtils.CreateAttribute(xmlManifest, "type", "Module"))

            'Folders Element
            Dim nodeFolders As XmlNode = xmlManifest.CreateElement("folders")
            nodeRoot.AppendChild(nodeFolders)

            'Folder Element
            Dim nodeFolder As XmlNode = xmlManifest.CreateElement("folder")
            nodeFolders.AppendChild(nodeFolder)

            'Desktop Module Info
            nodeFolder.AppendChild(XmlUtils.CreateElement(xmlManifest, "name", _Name))
            nodeFolder.AppendChild(XmlUtils.CreateElement(xmlManifest, "friendlyname", objDesktopModule.FriendlyName))
            nodeFolder.AppendChild(XmlUtils.CreateElement(xmlManifest, "foldername", objDesktopModule.FolderName))
            nodeFolder.AppendChild(XmlUtils.CreateElement(xmlManifest, "modulename", _Name))
            nodeFolder.AppendChild(XmlUtils.CreateElement(xmlManifest, "description", objDesktopModule.Description))
            If objDesktopModule.Version = Null.NullString Then
                objDesktopModule.Version = "01.00.00"
            End If
            nodeFolder.AppendChild(XmlUtils.CreateElement(xmlManifest, "version", objDesktopModule.Version))
            nodeFolder.AppendChild(XmlUtils.CreateElement(xmlManifest, "businesscontrollerclass", objDesktopModule.BusinessControllerClass))
            If objDesktopModule.CompatibleVersions <> "" Then
                nodeFolder.AppendChild(XmlUtils.CreateElement(xmlManifest, "compatibleversions", objDesktopModule.CompatibleVersions))
            End If
            If objDesktopModule.Dependencies <> "" Then
                nodeFolder.AppendChild(XmlUtils.CreateElement(xmlManifest, "dependencies", objDesktopModule.Dependencies))
            End If
            If objDesktopModule.Permissions <> "" Then
                nodeFolder.AppendChild(XmlUtils.CreateElement(xmlManifest, "permissions", objDesktopModule.Permissions))
            End If
            If SupportsProbingPrivatePath Then
                nodeFolder.AppendChild(XmlUtils.CreateElement(xmlManifest, "supportsprobingprivatepath", SupportsProbingPrivatePath.ToString))
            End If

            'Add Source files
            If IncludeSource Then
                nodeFolder.AppendChild(XmlUtils.CreateElement(xmlManifest, "resourcefile", ResourceFileName))
            End If

            'Modules Element
            Dim nodeModules As XmlNode = xmlManifest.CreateElement("modules")
            nodeFolder.AppendChild(nodeModules)

            'Get the Module Definitions for this Module
            Dim objModuleDefinitionController As New ModuleDefinitionController
            Dim arrModuleDefinitions As ArrayList = objModuleDefinitionController.GetModuleDefinitions(objDesktopModule.DesktopModuleID)

            'Iterate through Module Definitions
            For Each objModuleInfo As ModuleDefinitionInfo In arrModuleDefinitions
                Dim nodeModule As XmlNode = xmlManifest.CreateElement("module")

                'Add module definition properties
                nodeModule.AppendChild(XmlUtils.CreateElement(xmlManifest, "friendlyname", objModuleInfo.FriendlyName))

                'Add Cache properties
                nodeModule.AppendChild(XmlUtils.CreateElement(xmlManifest, "cachetime", objModuleInfo.DefaultCacheTime.ToString))

                'Get the Module Controls for this Module Definition
                Dim arrModuleControls As ArrayList = ModuleControlController.GetModuleControls(objModuleInfo.ModuleDefID)

                'Controls Element
                Dim nodeControls As XmlNode = xmlManifest.CreateElement("controls")
                nodeModule.AppendChild(nodeControls)

                'Iterate through Module Controls
                For Each objModuleControl As ModuleControlInfo In arrModuleControls
                    Dim nodeControl As XmlNode = xmlManifest.CreateElement("control")

                    'Add module control properties
                    XmlUtils.AppendElement(xmlManifest, nodeControl, "key", objModuleControl.ControlKey, False)
                    XmlUtils.AppendElement(xmlManifest, nodeControl, "title", objModuleControl.ControlTitle, False)

                    XmlUtils.AppendElement(xmlManifest, nodeControl, "src", objModuleControl.ControlSrc, True)
                    XmlUtils.AppendElement(xmlManifest, nodeControl, "iconfile", objModuleControl.IconFile, False)
                    XmlUtils.AppendElement(xmlManifest, nodeControl, "type", objModuleControl.ControlType.ToString, True)
                    XmlUtils.AppendElement(xmlManifest, nodeControl, "helpurl", objModuleControl.HelpURL, False)

                    If objModuleControl.SupportsPartialRendering Then
                        XmlUtils.AppendElement(xmlManifest, nodeControl, "supportspartialrendering", "true", False)
                    End If

                    'Add control Node to controls
                    nodeControls.AppendChild(nodeControl)

                    'Determine the filename for the Manifest file (It should be saved with the other Module files)
                    If filename = "" Then
                        filename = Folder & "\" & objDesktopModule.ModuleName + ".dnn"
                    End If
                Next

                'Add module Node to modules
                nodeModules.AppendChild(nodeModule)
            Next

            'Files Element
            Dim nodeFiles As XmlNode = xmlManifest.CreateElement("files")
            nodeFolder.AppendChild(nodeFiles)

            'Add the files
            For Each file As PaFileInfo In _Files
                Dim nodeFile As XmlNode = xmlManifest.CreateElement("file")

                'Add file properties
                XmlUtils.AppendElement(xmlManifest, nodeFile, "path", file.Path, False)
                XmlUtils.AppendElement(xmlManifest, nodeFile, "name", file.Name, False)

                'Add file Node to files
                nodeFiles.AppendChild(nodeFile)
            Next

            'Add Root element to document
            xmlManifest.AppendChild(nodeRoot)

            'Save Manifest file
            xmlManifest.Save(filename)

        End Sub

        Private Sub CreateFileList()

            'Create the DirectoryInfo object
            Dim folderInfo As New DirectoryInfo(Folder)

            'Get the Project File in the folder
            Dim files As FileInfo() = folderInfo.GetFiles("*.??proj")

            If files.Length = 0 Then 'Assume Dynamic (App_Code based) Module

                'Add the files in the DesktopModules Folder
                ParseFolder(Folder, Folder)

                'Add the files in the AppCode Folder
                ParseFolder(AppCodeFolder, AppCodeFolder)

            Else 'WAP Project File is present

                'Parse the Project files (probably only one)
                For Each projFile As FileInfo In files
                    ParseProject(projFile)
                Next

            End If
        End Sub

        Private Function CreateZipFile() As String
            Dim CompressionLevel As Integer = 9
            Dim ZipFileShortName As String = _Name
            Dim ZipFileName As String = _ZipFile
            If ZipFileName = "" Then
                ZipFileName = ZipFileShortName & ".zip"
            End If
            ZipFileName = Common.Globals.ApplicationMapPath & "\Install\Module\" & ZipFileName

            Dim strmZipFile As FileStream = Nothing
            Try
                ProgressLog.AddInfo(String.Format(Localization.GetString("LOG.PAWriter.CreateArchive"), ZipFileShortName))
                strmZipFile = File.Create(ZipFileName)
                Dim strmZipStream As ZipOutputStream = Nothing
                Try
                    strmZipStream = New ZipOutputStream(strmZipFile)
                    strmZipStream.SetLevel(CompressionLevel)
                    For Each PaFile As PaFileInfo In _Files
                        If File.Exists(PaFile.FullName) Then
                            FileSystemUtils.AddToZip(strmZipStream, PaFile.FullName, PaFile.Name, "")
                            ProgressLog.AddInfo(String.Format(Localization.GetString("LOG.PAWriter.SavedFile"), PaFile.Name))
                        End If
                    Next
                Catch ex As Exception
                    LogException(ex)
                    ProgressLog.AddFailure(String.Format(Localization.GetString("LOG.PAWriter.ERROR.SavingFile"), ex))
                Finally
                    If Not strmZipStream Is Nothing Then
                        strmZipStream.Finish()
                        strmZipStream.Close()
                    End If
                End Try
            Catch ex As Exception
                LogException(ex)
                ProgressLog.AddFailure(String.Format(Localization.GetString("LOG.PAWriter.ERROR.SavingFile"), ex))
            Finally
                If Not strmZipFile Is Nothing Then
                    strmZipFile.Close()
                End If
            End Try

            Return ZipFileName
        End Function

#Region "WAP Methods"

        Private Sub AddFile(ByVal xmlFile As XmlNode)
            Dim relPath As String = xmlFile.Attributes("Include").Value.Replace("/", "\")
            Dim path As String = ""
            Dim name As String = relPath
            Dim fullPath As String = Folder
            If relPath.LastIndexOf("\") > -1 Then
                path = relPath.Substring(0, relPath.LastIndexOf("\"))
                name = relPath.Replace(path & "\", "")
                fullPath = fullPath & "\" & path
            End If

            AddFile(New PaFileInfo(name, path, fullPath))
        End Sub

        Private Sub AddSourceFiles(ByVal folderInfo As DirectoryInfo, ByVal fileType As String, ByRef resourcesFile As ZipOutputStream)

            'Get the Source Files in the folder
            Dim sourceFiles As FileInfo() = folderInfo.GetFiles(fileType)

            For Each sourceFile As FileInfo In sourceFiles
                Dim filePath As String = sourceFile.FullName
                Dim fileName As String = sourceFile.Name
                Dim folderName As String = folderInfo.FullName.Replace(Folder, "")
                If folderName <> "" Then
                    folderName += "\"
                End If
                If folderName.StartsWith("\") Then
                    folderName = folderName.Substring(1)
                End If

                FileSystemUtils.AddToZip(resourcesFile, filePath, fileName, folderName)
            Next

        End Sub

        Private Sub CreateResourceFile()

            'Create the DirectoryInfo object
            Dim folderInfo As New DirectoryInfo(Folder)
            Dim filename As String = Folder & "\" & ResourceFileName

            'Create Zip File to hold files
            Dim resourcesFile As ZipOutputStream = New ZipOutputStream(File.Create(fileName))
            resourcesFile.SetLevel(6)

            ParseFolder(folderInfo, resourcesFile)

            'Add Resources File to File List
            AddFile(New PaFileInfo(filename.Replace(Folder & "\", ""), "", Folder))

            'Finish and Close Zip file
            resourcesFile.Finish()
            resourcesFile.Close()

        End Sub

        Private Sub ParseFolder(ByVal folder As DirectoryInfo, ByRef resourcesFile As ZipOutputStream)
            'Add the resource files
            AddSourceFiles(folder, "*.sln", resourcesFile)
            AddSourceFiles(folder, "*.??proj", resourcesFile)
            AddSourceFiles(folder, "*.cs", resourcesFile)
            AddSourceFiles(folder, "*.vb", resourcesFile)
            AddSourceFiles(folder, "*.resx", resourcesFile)

            'Check for Provider scripts
            Dim objProviderConfiguration As ProviderConfiguration = ProviderConfiguration.GetProviderConfiguration("data")
            For Each entry As DictionaryEntry In objProviderConfiguration.Providers
                Dim objProvider As Provider = CType(entry.Value, Provider)
                Dim providerName As String = objProvider.Name
                AddSourceFiles(folder, "*." & providerName, resourcesFile)
            Next

            'Get the sub-folders in the folder
            Dim folders As DirectoryInfo() = folder.GetDirectories()

            'Recursively call ParseFolder to add files from sub-folder tree
            For Each subfolder As DirectoryInfo In folders
                If Not subfolder.Name.ToLower().Contains("_sgbak") Then
                    ParseFolder(subfolder, resourcesFile)
                End If
            Next
        End Sub

        Private Sub ParseProject(ByVal projFile As FileInfo)

            Dim assemblyName As String = ""
            Dim assemblyFolder As String = ApplicationMapPath & "/bin"

            'Create and load the Project file xml
            Dim xmlProject As New XmlDocument
            xmlProject.Load(projFile.FullName)

            ' Create an XmlNamespaceManager to resolve the default namespace.
            Dim nsmgr As XmlNamespaceManager = New XmlNamespaceManager(xmlProject.NameTable)
            nsmgr.AddNamespace("proj", "http://schemas.microsoft.com/developer/msbuild/2003")

            'Get the Assembly Name and add to File List
            Dim xmlSettings As XmlNode = xmlProject.DocumentElement.SelectSingleNode("proj:PropertyGroup/proj:AssemblyName", nsmgr)
            assemblyName = xmlSettings.InnerText
            AddFile(New PaFileInfo(assemblyName & ".dll", "", assemblyFolder))

            'Add all the files that are classified as None
            For Each xmlFile As XmlNode In xmlProject.DocumentElement.SelectNodes("proj:ItemGroup/proj:None", nsmgr)
                AddFile(xmlFile)
            Next

            'Add all the files that are classified as Content
            For Each xmlFile As XmlNode In xmlProject.DocumentElement.SelectNodes("proj:ItemGroup/proj:Content", nsmgr)
                AddFile(xmlFile)
            Next
        End Sub

#End Region

#Region "App_Code Methods"

        Private Sub ParseFolder(ByVal folderName As String, ByVal rootPath As String)

            If Directory.Exists(folderName) Then
                Dim folder As DirectoryInfo = New DirectoryInfo(folderName)

                'Recursively parse the subFolders
                Dim subFolders As DirectoryInfo() = folder.GetDirectories()
                For Each subFolder As DirectoryInfo In subFolders
                    If Not subFolder.Name.ToLower().Contains("_sgbak") Then
                        ParseFolder(subFolder.FullName, rootPath)
                    End If
                Next

                'Add the Files in the Folder
                Dim files As FileInfo() = folder.GetFiles()
                For Each file As FileInfo In files
                    Dim path As String = folder.FullName.Replace(rootPath, "")
                    If path.StartsWith("\") Then
                        path = path.Substring(1)
                    End If
                    If folder.FullName.ToLower.Contains("app_code") Then
                        path = "[app_code]" + path
                    End If
                    AddFile(New PaFileInfo(file.Name, path, folder.FullName))
                Next
            End If

        End Sub

#End Region

#End Region

#Region "Public Methods"

        Public Function CreatePrivateAssembly(ByVal DesktopModuleId As Integer) As String

            Dim Result As String = ""

            'Get the Module Definition File for this Module
            Dim objDesktopModuleController As New DesktopModuleController
            Dim objModule As DesktopModuleInfo = objDesktopModuleController.GetDesktopModule(DesktopModuleId)
            _Folder = Common.Globals.ApplicationMapPath & "\DesktopModules\" & objModule.FolderName
            _AppCodeFolder = Common.Globals.ApplicationMapPath & "\App_Code\" & objModule.FolderName

            If IncludeSource Then
                _ResourceFileName = objModule.ModuleName & "_Source.zip"
                CreateResourceFile()
            End If

            'Create File List
            CreateFileList()

            If CreateManifest Then
                ProgressLog.StartJob(String.Format(Localization.GetString("LOG.PAWriter.CreateManifest"), objModule.FriendlyName))
                CreateDnnManifest(objModule)
                ProgressLog.EndJob((String.Format(Localization.GetString("LOG.PAWriter.CreateManifest"), objModule.FriendlyName)))
            End If

            'Always add Manifest file to file list
            AddFile(New PaFileInfo(objModule.ModuleName & ".dnn", "", Folder), True)

            ProgressLog.StartJob(String.Format(Localization.GetString("LOG.PAWriter.CreateZipFile"), objModule.FriendlyName))
            CreateZipFile()
            ProgressLog.EndJob((String.Format(Localization.GetString("LOG.PAWriter.CreateZipFile"), objModule.FriendlyName)))

            Return Result
        End Function

#End Region

    End Class
End Namespace