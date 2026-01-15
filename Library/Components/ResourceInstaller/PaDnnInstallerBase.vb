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
Imports DotNetNuke.Entities.Modules.Actions
Imports DotNetNuke.Entities.Modules.Definitions
Imports System.Text.RegularExpressions

Namespace DotNetNuke.Modules.Admin.ResourceInstaller

    ''' -----------------------------------------------------------------------------
    ''' <summary>
    ''' The PaDnnInstallerBase is a base class for all PaDnnInstallers
    ''' </summary>
    ''' <remarks>
    ''' </remarks>
    ''' <history>
    ''' 	[cnurse]	05/09/2005  documented
    ''' </history>
    ''' -----------------------------------------------------------------------------
    Public Class PaDnnInstallerBase
        Inherits ResourceInstallerBase

#Region "Private Members"

        Private _installerInfo As PaInstallInfo
        Private _upgradeversions As ArrayList

#End Region

#Region "Constructors"

        Public Sub New(ByVal InstallerInfo As PaInstallInfo)
            _installerInfo = InstallerInfo
        End Sub

#End Region

#Region "Properties"

        Public ReadOnly Property InstallerInfo() As PaInstallInfo
            Get
                Return _installerInfo
            End Get
        End Property

        Public Property UpgradeVersions() As ArrayList
            Get
                If _upgradeversions Is Nothing Then
                    _upgradeversions = New ArrayList
                End If
                Return _upgradeversions
            End Get
            Set(ByVal Value As ArrayList)
                _upgradeversions = Value
            End Set
        End Property

#End Region

#Region "Protected Methods"

        Protected Overridable Function BatchSql(ByVal sqlFile As PaFile) As Boolean

            Dim WasSuccessful As Boolean = True

            InstallerInfo.Log.StartJob(String.Format(SQL_BeginFile, sqlFile.Name))

            Dim strScript As String = ""
            Select Case sqlFile.Encoding
                Case PaTextEncoding.UTF16LittleEndian
                    strScript = GetAsciiString(sqlFile.Buffer, System.Text.Encoding.Unicode)     'System.Text.Encoding.Unicode.GetString(sqlFile.Buffer)
                Case PaTextEncoding.UTF16BigEndian
                    strScript = GetAsciiString(sqlFile.Buffer, System.Text.Encoding.BigEndianUnicode)     'System.Text.Encoding.BigEndianUnicode.GetString(sqlFile.Buffer)
                Case PaTextEncoding.UTF8
                    strScript = GetAsciiString(sqlFile.Buffer, System.Text.Encoding.UTF8)     'System.Text.Encoding.UTF8.GetString(sqlFile.Buffer)
                Case PaTextEncoding.UTF7
                    strScript = GetAsciiString(sqlFile.Buffer, System.Text.Encoding.UTF7)     'System.Text.Encoding.UTF7.GetString(sqlFile.Buffer)
                Case PaTextEncoding.Unknown
                    Throw New Exception(String.Format(SQL_UnknownFile, sqlFile.Name))
            End Select

            'This check needs to be included because the unicode Byte Order mark results in an extra character at the start of the file
            'The extra character - '?' - causes an error with the database.
            If strScript.StartsWith("?") Then
                strScript = strScript.Substring(1)
            End If

            ' execute SQL installation script
            'TODO: Transactions are removed temporarily.
            Dim strSQLExceptions As String = PortalSettings.ExecuteScript(strScript, UseTransactions:=False)

            If strSQLExceptions <> "" Then
                InstallerInfo.Log.AddFailure(String.Format(SQL_Exceptions, vbCrLf, strSQLExceptions))
                WasSuccessful = False
            End If

            InstallerInfo.Log.EndJob(String.Format(SQL_EndFile, sqlFile.Name))

            Return WasSuccessful
        End Function

        Protected Overridable Sub CreateBinFile(ByVal File As PaFile)
            Dim binFolder As String = Path.Combine(InstallerInfo.SitePath, "bin")
            Dim binFullFileName As String = Path.Combine(binFolder, File.Name)
            CreateFile(binFullFileName, File.Buffer)
            InstallerInfo.Log.AddInfo((FILE_Created + binFullFileName))
        End Sub

        Protected Overridable Sub CreatePrivateBinFile(ByVal File As PaFile)
            Dim binFolder As String = Path.Combine(InstallerInfo.SitePath, "bin\Modules")

            ' create the private folder
            If Not Directory.Exists(binFolder) Then
                Directory.CreateDirectory(binFolder)
            End If

            Dim binFullFileName As String = Path.Combine(binFolder, File.Name)
            CreateFile(binFullFileName, File.Buffer)
            InstallerInfo.Log.AddInfo((FILE_Created + binFullFileName))
        End Sub

        Protected Overridable Sub CreateDataProviderFile(ByVal file As PaFile, ByVal Folder As PaFolder)
            Dim rootFolder As String = Path.Combine(InstallerInfo.SitePath, Path.Combine("DesktopModules", Folder.FolderName))
            Dim ProviderTypeFolder As String = Path.Combine("Providers\DataProviders", file.Extension)
            Dim ProviderFolder As String = Path.Combine(rootFolder, ProviderTypeFolder)

            If Not Directory.Exists(ProviderFolder) Then
                Directory.CreateDirectory(ProviderFolder)
            End If

            ' save file
            Dim FullFileName As String = Path.Combine(ProviderFolder, file.Name)
            CreateFile(FullFileName, file.Buffer)
            InstallerInfo.Log.AddInfo((FILE_Created + FullFileName))

        End Sub

        Protected Sub CreateFile(ByVal FullFileName As String, ByVal Buffer As Byte())
            If System.IO.File.Exists(FullFileName) Then
                System.IO.File.SetAttributes(FullFileName, FileAttributes.Normal)
            End If
            Dim fs As New FileStream(FullFileName, FileMode.Create, FileAccess.Write)
            fs.Write(Buffer, 0, Buffer.Length)
            fs.Close()
        End Sub

        Protected Overridable Sub CreateFiles(ByVal Folder As PaFolder)

            InstallerInfo.Log.StartJob(FILES_Creating)

            ' create the files
            Dim file As PaFile
            For Each file In Folder.Files
                Select Case file.Type
                    Case PaFileType.DataProvider
                        ' We need to store uninstall files in the main module directory because
                        ' that is where the uninstaller expects to find them
                        If file.Name.ToLower.IndexOf("uninstall") <> -1 Then
                            CreateModuleFile(file, Folder)
                        Else
                            CreateDataProviderFile(file, Folder)
                        End If
                    Case PaFileType.Dll
                        If Folder.SupportsProbingPrivatePath Then
                            CreatePrivateBinFile(file)
                        Else
                            CreateBinFile(file)
                        End If
                    Case PaFileType.Dnn, PaFileType.Ascx, PaFileType.Sql, PaFileType.Other
                        CreateModuleFile(file, Folder)
                End Select
            Next file

            InstallerInfo.Log.EndJob(FILES_Created)

        End Sub

        Protected Overridable Sub CreateModuleFile(ByVal File As PaFile, ByVal Folder As PaFolder)
            ' account for skinobject names which include [ ]
            Dim FolderName As String = Folder.FolderName.Trim("["c).Trim("]"c)

            Dim rootFolder As String = ""

            ' check for the [app_code] token ( dynamic modules )
            If File.Path.ToLower.StartsWith("[app_code]") Then
                rootFolder = Path.Combine(InstallerInfo.SitePath, Path.Combine("App_Code", FolderName))
                ' remove the [app_code] token
                If File.Path.ToLower = "[app_code]" Then
                    File.Path = ""
                Else ' there is extra path info to retain
                    File.Path = File.Path.Substring(10)
                End If
                Config.AddCodeSubDirectory(FolderName)
            Else
                rootFolder = Path.Combine(InstallerInfo.SitePath, Path.Combine("DesktopModules", FolderName))
            End If

            ' create the root folder
            If Not Directory.Exists(rootFolder) Then
                Directory.CreateDirectory(rootFolder)
            End If

            'if this is a Resource file, then we need to expand the resourcefile
            If Not Folder.ResourceFile Is Nothing AndAlso File.Name.ToLower.Equals(Folder.ResourceFile.ToLower) Then
                CreateResourceFile(File, rootFolder)
            Else
                ' create the actual file folder which includes any relative filepath info
                Dim fileFolder As String = Path.Combine(rootFolder, File.Path)
                If Not Directory.Exists(fileFolder) Then
                    Directory.CreateDirectory(fileFolder)
                End If

                ' save file
                Dim FullFileName As String = Path.Combine(fileFolder, File.Name)
                If File.Type = PaFileType.Dnn Then
                    FullFileName += ".config" ' add a forbidden extension so that it is not browsable
                End If
                CreateFile(FullFileName, File.Buffer)
                InstallerInfo.Log.AddInfo((FILE_Created + FullFileName))
            End If
        End Sub

        Protected Overridable Sub CreateResourceFile(ByVal ResourceFile As PaFile, ByVal RootFolder As String)

            InstallerInfo.Log.StartJob(FILES_Expanding)

            Try
                Dim objZipInputStream As New ZipInputStream(New MemoryStream(ResourceFile.Buffer))
                FileSystemUtils.UnzipResources(objZipInputStream, RootFolder)
            Catch ex As Exception

            End Try

            InstallerInfo.Log.EndJob(FILES_CreatedResources)
        End Sub

        Protected Overridable Sub ExecuteSql(ByVal Folder As PaFolder)

            InstallerInfo.Log.StartJob(SQL_Begin)
            ' get list of script files
            Dim arrScriptFiles As New ArrayList
            Dim InstallScript As PaFile = Nothing

            ' executing all the sql files
            Dim file As PaFile
            For Each file In Folder.Files
                ' DataProvider files may be either: the SQL to execute, uninstall, or XML stored procs.
                ' We only want to execute the first type of DataProvider files.
                If file.Type = PaFileType.Sql _
                 OrElse (file.Type = PaFileType.DataProvider And file.Name.ToLower.IndexOf("uninstall") = -1 And file.Name.ToLower.IndexOf(".xml") = -1) Then

                    Dim objProviderConfiguration As Framework.Providers.ProviderConfiguration = Framework.Providers.ProviderConfiguration.GetProviderConfiguration("data")

                    If objProviderConfiguration.DefaultProvider.ToLower = Path.GetExtension(file.Name.ToLower).Substring(1) Then
                        If file.Name.ToLower.StartsWith("install.") Then
                            InstallScript = file
                        Else
                            arrScriptFiles.Add(file)
                        End If
                    End If
                End If
            Next file

            ' get current module version
            Dim strModuleVersion As String = "000000"
            Dim objDesktopModule As DesktopModuleInfo = GetDesktopModule(Folder)
            If Not objDesktopModule Is Nothing Then
                strModuleVersion = objDesktopModule.Version.Replace(".", "")
            End If
            Dim strScriptVersion As String

            If Not InstallScript Is Nothing And objDesktopModule Is Nothing Then
                ' new install
                InstallerInfo.Log.AddInfo(SQL_Executing + InstallScript.Name)
                BatchSql(InstallScript)

                Dim strInstallVersion As String
                strInstallVersion = Path.GetFileNameWithoutExtension(InstallScript.Name).Replace(".", "")
                strInstallVersion = strInstallVersion.ToLower.Replace("install", "")

                ' if install script includes version number will be used a base version for upgrades
                ' otherwise it is assigned an initial version of 000000
                If strInstallVersion <> "" Then
                    strModuleVersion = strInstallVersion
                End If
                UpgradeVersions.Add(System.Text.RegularExpressions.Regex.Replace(strModuleVersion, "^(?<a>\d{2})(?<b>\d{2})(?<c>\d{2})$", "${a}.${b}.${c}"))
            End If

            ' iterate through scripts
            Dim Comparer As New PaDataProviderComparer
            arrScriptFiles.Sort(Comparer)
            Dim scriptFile As PaFile
            For Each scriptFile In arrScriptFiles
                strScriptVersion = Path.GetFileNameWithoutExtension(scriptFile.Name).Replace(".", "")
                If strScriptVersion > strModuleVersion Then
                    UpgradeVersions.Add(Path.GetFileNameWithoutExtension(scriptFile.Name))
                    InstallerInfo.Log.AddInfo((SQL_Executing + scriptFile.Name))
                    BatchSql(scriptFile)
                End If
            Next

            InstallerInfo.Log.EndJob(SQL_End)
        End Sub

        Protected Function GetAsciiString(ByVal Buffer As Byte(), ByVal SourceEncoding As Encoding) As String

            ' Create two different encodings.
            Dim TargetEncoding As Encoding = Encoding.ASCII

            ' Perform the conversion from one encoding to the other.
            Dim asciiBytes As Byte() = Encoding.Convert(SourceEncoding, TargetEncoding, Buffer)

            ' Convert the new byte[] into an ascii string.
            Dim asciiString As String = System.Text.Encoding.ASCII.GetString(asciiBytes)

            Return asciiString
        End Function

        Protected Overridable Function GetDesktopModuleSettings(ByVal objDesktopModule As DesktopModuleInfo, ByVal Folder As PaFolder) As DesktopModuleInfo
            objDesktopModule.FriendlyName = Folder.FriendlyName
            objDesktopModule.FolderName = Folder.FolderName
            objDesktopModule.ModuleName = Folder.ModuleName
            objDesktopModule.Description = Folder.Description
            objDesktopModule.Version = Folder.Version

            Return objDesktopModule
        End Function

        Protected Overridable Function GetDesktopModule(ByVal Folder As PaFolder) As DesktopModuleInfo

            Dim objDesktopModules As New DesktopModuleController
            Dim objDesktopModule As DesktopModuleInfo = objDesktopModules.GetDesktopModuleByFriendlyName(Folder.FriendlyName)

            Return objDesktopModule

        End Function

        Protected Function GetModDefID(ByVal TempModDefID As Integer, ByVal Modules As ArrayList) As Integer
            Dim ModDefID As Integer = -1

            Dim MI As ModuleDefinitionInfo
            For Each MI In Modules
                If MI.TempModuleID = TempModDefID Then
                    ModDefID = MI.ModuleDefID
                    Exit For
                End If
            Next
            Return ModDefID
        End Function

        Protected Overridable Sub RegisterModules(ByVal Folder As PaFolder, ByVal Modules As ArrayList, ByVal Controls As ArrayList)

            InstallerInfo.Log.StartJob(REGISTER_Module)

            Dim objDesktopModules As New DesktopModuleController

            ' check if desktop module exists
            Dim objDesktopModule As DesktopModuleInfo = GetDesktopModule(Folder)
            If objDesktopModule Is Nothing Then
                objDesktopModule = New DesktopModuleInfo
                objDesktopModule.DesktopModuleID = Null.NullInteger
                objDesktopModule.SupportedFeatures = Null.NullInteger
            End If

            objDesktopModule = GetDesktopModuleSettings(objDesktopModule, Folder)

            If Null.IsNull(objDesktopModule.DesktopModuleID) Then
                ' new desktop module
                objDesktopModule.DesktopModuleID = objDesktopModules.AddDesktopModule(objDesktopModule)
            Else
                ' existing desktop module
                objDesktopModules.UpdateDesktopModule(objDesktopModule, False)
            End If

            InstallerInfo.Log.AddInfo(REGISTER_Definition)

            Dim objModuleDefinitons As New ModuleDefinitionController

            Dim objModuleDefinition As ModuleDefinitionInfo
            For Each objModuleDefinition In Modules
                ' check if definition exists
                Dim objModuleDefinition2 As ModuleDefinitionInfo = objModuleDefinitons.GetModuleDefinitionByName(objDesktopModule.DesktopModuleID, objModuleDefinition.FriendlyName)
                If objModuleDefinition2 Is Nothing Then
                    ' add new definition
                    objModuleDefinition.DesktopModuleID = objDesktopModule.DesktopModuleID
                    objModuleDefinition.ModuleDefID = objModuleDefinitons.AddModuleDefinition(objModuleDefinition)
                Else
                    ' update existing definition
                    objModuleDefinition.ModuleDefID = objModuleDefinition2.ModuleDefID
                    objModuleDefinitons.UpdateModuleDefinition(objModuleDefinition, False)

                End If
            Next

            InstallerInfo.Log.AddInfo(REGISTER_Controls)

            Dim objModuleControl As ModuleControlInfo
            For Each objModuleControl In Controls
                ' get the real ModuleDefID from the associated Module
                objModuleControl.ModuleDefID = GetModDefID(objModuleControl.ModuleDefID, Modules)

                ' check if control exists
                Dim objModuleControl2 As ModuleControlInfo = ModuleControlController.GetModuleControlByKeyAndSrc(objModuleControl.ModuleDefID, objModuleControl.ControlKey, objModuleControl.ControlSrc)
                If objModuleControl2 Is Nothing Then
                    ' add new control
                    ModuleControlController.AddModuleControl(objModuleControl)
                Else
                    ' update existing control 
                    objModuleControl.ModuleControlID = objModuleControl2.ModuleControlID
                    ModuleControlController.UpdateModuleControl(objModuleControl, False)
                End If
            Next

			InstallerInfo.Log.EndJob(REGISTER_End)

            UpgradeModule(objDesktopModule)

        End Sub

        Protected Overridable Function UpgradeModule(ByVal ModuleInfo As DesktopModuleInfo) As String
            Return Nothing
        End Function

        Protected Function ValidateVersion(ByVal Folder As PaFolder) As Boolean

            ' check if desktop module exists
            Dim objDesktopModule As DesktopModuleInfo = GetDesktopModule(Folder)
            If Not objDesktopModule Is Nothing Then
                If objDesktopModule.Version > Folder.Version Then
                    Return False
                End If
            End If

            Return True

        End Function

        Protected Function ValidateCompatibility(ByVal Folder As PaFolder) As Boolean

            ' check core framework compatibility
            If Folder.CompatibleVersions <> "" Then
                Try
                    Dim objMatch As Match
                    objMatch = Regex.Match(glbAppVersion, Folder.CompatibleVersions, RegexOptions.IgnoreCase Or RegexOptions.Singleline)
                    If objMatch Is Match.Empty Then
                        Return False
                    Else
                        Return True
                    End If
                Catch ' RegExp expression is not valid
                    Return False
                End Try
            End If

            Return True

        End Function

        Protected Function ValidateDependencies(ByVal Folder As PaFolder) As Boolean
            If Folder.Dependencies <> "" Then
                For Each strDependency As String In (Folder.Dependencies & ";").Split(Convert.ToChar(";"))
                    If strDependency.Trim <> "" Then
                        If Framework.Reflection.CreateType(strDependency, True) Is Nothing Then
                            Return False
                        End If
                    End If
                Next
            End If

            Return True

        End Function

        Protected Function ValidatePermissions(ByVal Folder As PaFolder) As Boolean
            Dim strPermission As String = Null.NullString
            Return Framework.SecurityPolicy.HasPermissions(Folder.Permissions, strPermission)
        End Function

#End Region

#Region "Public Methods"

        Public Overridable Sub Install(ByVal folders As PaFolderCollection)
            Dim folder As PaFolder
            For Each folder In folders

                Dim blnInstall As Boolean = True

                If ValidateCompatibility(folder) = False Then
                    InstallerInfo.Log.AddWarning(INSTALL_Compatibility)
                    blnInstall = False
                End If

                If ValidateVersion(folder) = False Then
                    InstallerInfo.Log.AddWarning(INSTALL_OlderVersion)
                    blnInstall = False
                End If

                If ValidateDependencies(folder) = False Then
                    InstallerInfo.Log.AddWarning(INSTALL_Dependencies)
                    blnInstall = False
                End If

                If ValidatePermissions(folder) = False Then
                    InstallerInfo.Log.AddWarning(INSTALL_Permissions)
                    blnInstall = False
                End If

                If blnInstall Then
                    ExecuteSql(folder)
                    CreateFiles(folder)
                    If folder.Modules.Count > 0 Then
                        RegisterModules(folder, folder.Modules, folder.Controls)
                    End If
                End If
			Next
			DataCache.ClearModuleCache()

        End Sub

#End Region

    End Class

End Namespace
