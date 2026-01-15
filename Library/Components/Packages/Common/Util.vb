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
Imports System.Collections
Imports System.IO
Imports System.Xml.XPath

Imports DotNetNuke.Entities.Modules
Imports DotNetNuke.Services.Localization


Namespace DotNetNuke.Services.Packages

    ''' -----------------------------------------------------------------------------
    ''' <summary>
    ''' The InstallerBase class is a Base Class for all Installer
    '''	classes that need to use Localized Strings.  It provides these strings
    '''	as localized Constants.
    ''' </summary>
    ''' <remarks>
    ''' </remarks>
    ''' <history>
    ''' 	[cnurse]	07/05/2007	created
    ''' </history>
    ''' -----------------------------------------------------------------------------
    Public Class Util

#Region "Public Constants"

        Public Shared ASSEMBLY_Added As String = GetLocalizedString("ASSEMBLY_Added")
        Public Shared ASSEMBLY_InUse As String = GetLocalizedString("ASSEMBLY_InUse")
        Public Shared ASSEMBLY_Registered As String = GetLocalizedString("ASSEMBLY_Registered")
        Public Shared ASSEMBLY_UnRegistered As String = GetLocalizedString("ASSEMBLY_UnRegistered")
        Public Shared ASSEMBLY_Updated As String = GetLocalizedString("ASSEMBLY_Updated")

        Public Shared AUTHENTICATION_ReadSuccess As String = GetLocalizedString("AUTHENTICATION_ReadSuccess")
        Public Shared AUTHENTICATION_LoginSrcMissing As String = GetLocalizedString("AUTHENTICATION_LoginSrcMissing")
        Public Shared AUTHENTICATION_Registered As String = GetLocalizedString("AUTHENTICATION_Registered")
        Public Shared AUTHENTICATION_SettingsSrcMissing As String = GetLocalizedString("AUTHENTICATION_SettingsSrcMissing")
        Public Shared AUTHENTICATION_TypeMissing As String = GetLocalizedString("AUTHENTICATION_TypeMissing")
        Public Shared AUTHENTICATION_UnRegistered As String = GetLocalizedString("AUTHENTICATION_UnRegistered")

        Public Shared COMPONENT_Installed As String = GetLocalizedString("COMPONENT_Installed")
        Public Shared COMPONENT_RolledBack As String = GetLocalizedString("COMPONENT_RolledBack")
        Public Shared COMPONENT_RollingBack As String = GetLocalizedString("COMPONENT_RollingBack")
        Public Shared COMPONENT_UnInstalled As String = GetLocalizedString("COMPONENT_UnInstalled")

        Public Shared CONFIG_Committed As String = GetLocalizedString("CONFIG_Committed")
        Public Shared CONFIG_RolledBack As String = GetLocalizedString("CONFIG_RolledBack")
        Public Shared CONFIG_Updated As String = GetLocalizedString("CONFIG_Updated")

        Public Shared DNN_Reading As String = GetLocalizedString("DNN_Reading")
        Public Shared DNN_ReadingComponent As String = GetLocalizedString("DNN_ReadingComponent")
        Public Shared DNN_ReadingPackage As String = GetLocalizedString("DNN_ReadingPackage")
        Public Shared DNN_Success As String = GetLocalizedString("DNN_Success")
        'Public Shared DNN_Valid As String = GetLocalizedString("DNN_Valid")
        'Public Shared DNN_ValidSkinObject As String = GetLocalizedString("DNN_ValidSkinObject")

        Public Shared EXCEPTION As String = GetLocalizedString("EXCEPTION")
        Public Shared EXCEPTION_NameMissing As String = GetLocalizedString("EXCEPTION_NameMissing")
        Public Shared EXCEPTION_TypeMissing As String = GetLocalizedString("EXCEPTION_TypeMissing")
        Public Shared EXCEPTION_VersionMissing As String = GetLocalizedString("EXCEPTION_VersionMissing")
        'Public Shared EXCEPTION_DesktopSrc As String = GetLocalizedString("EXCEPTION_DesktopSrc")
        Public Shared EXCEPTION_FileLoad As String = GetLocalizedString("EXCEPTION_FileLoad")
        'Public Shared EXCEPTION_FileName As String = GetLocalizedString("EXCEPTION_FileName")
        Public Shared EXCEPTION_FileRead As String = GetLocalizedString("EXCEPTION_FileRead")
        'Public Shared EXCEPTION_FolderDesc As String = GetLocalizedString("EXCEPTION_FolderDesc")
        'Public Shared EXCEPTION_FolderName As String = GetLocalizedString("EXCEPTION_FolderName")
        'Public Shared EXCEPTION_FolderProvider As String = GetLocalizedString("EXCEPTION_FolderProvider")
        'Public Shared EXCEPTION_FolderVersion As String = GetLocalizedString("EXCEPTION_FolderVersion")
        'Public Shared EXCEPTION_Format As String = GetLocalizedString("EXCEPTION_LoadFailed")
        'Public Shared EXCEPTION_LoadFailed As String = GetLocalizedString("EXCEPTION_LoadFailed")
        Public Shared EXCEPTION_InstallerCreate As String = GetLocalizedString("EXCEPTION_InstallerCreate")
        Public Shared EXCEPTION_MissingDnn As String = GetLocalizedString("EXCEPTION_MissingDnn")
        'Public Shared EXCEPTION_MissingResource As String = GetLocalizedString("EXCEPTION_MissingResource")
        Public Shared EXCEPTION_MultipleDnn As String = GetLocalizedString("EXCEPTION_MultipleDnn")
        'Public Shared EXCEPTION_Src As String = GetLocalizedString("EXCEPTION_Src")
        'Public Shared EXCEPTION_Type As String = GetLocalizedString("EXCEPTION_Type")

        Public Shared FILE_CreateBackup As String = GetLocalizedString("FILE_CreateBackup")
        Public Shared FILE_Created As String = GetLocalizedString("FILE_Created")
        Public Shared FILE_Deleted As String = GetLocalizedString("FILE_Deleted")
        Public Shared FILE_Found As String = GetLocalizedString("FILE_Found")
        Public Shared FILE_Loading As String = GetLocalizedString("FILE_Loading")
        Public Shared FILE_NotFound As String = GetLocalizedString("FILE_NotFound")
        Public Shared FILE_ReadSuccess As String = GetLocalizedString("FILE_ReadSuccess")
        Public Shared FILE_RestoreBackup As String = GetLocalizedString("FILE_RestoreBackup")
        Public Shared FILE_RolledBack As String = GetLocalizedString("FILE_RolledBack")

        'Public Shared FILES_Created As String = GetLocalizedString("FILES_Created")
        Public Shared FILES_CreatedResources As String = GetLocalizedString("FILES_CreatedResources")
        'Public Shared FILES_Creating As String = GetLocalizedString("FILES_Creating")
        Public Shared FILES_Expanding As String = GetLocalizedString("FILES_Expanding")
        Public Shared FILES_Loading As String = GetLocalizedString("FILES_Loading")
        Public Shared FILES_Reading As String = GetLocalizedString("FILES_Reading")
        Public Shared FILES_ReadingEnd As String = GetLocalizedString("FILES_ReadingEnd")

        Public Shared FOLDER_Created As String = GetLocalizedString("FOLDER_Created")
        Public Shared FOLDER_DeletedBackup As String = GetLocalizedString("FOLDER_DeletedBackup")

        Public Shared INSTALL_Compatibility As String = GetLocalizedString("INSTALL_Compatibility")
        Public Shared INSTALL_Aborted As String = GetLocalizedString("INSTALL_Aborted")
        Public Shared INSTALL_Failed As String = GetLocalizedString("INSTALL_Failed")
        Public Shared INSTALL_Committed As String = GetLocalizedString("INSTALL_Committed")
        Public Shared INSTALL_Namespace As String = GetLocalizedString("INSTALL_Namespace")
        Public Shared INSTALL_Package As String = GetLocalizedString("INSTALL_Package")
        Public Shared INSTALL_Permissions As String = GetLocalizedString("INSTALL_Permissions")
        Public Shared INSTALL_Start As String = GetLocalizedString("INSTALL_Start")
        Public Shared INSTALL_Success As String = GetLocalizedString("INSTALL_Success")
        Public Shared INSTALL_Version As String = GetLocalizedString("INSTALL_Version")

        'Public Shared MODULES_ControlInfo As String = GetLocalizedString("MODULES_ControlInfo")
        'Public Shared MODULES_Loading As String = GetLocalizedString("MODULES_Loading")

        'Public Shared REGISTER_Controls As String = GetLocalizedString("REGISTER_Controls")
        'Public Shared REGISTER_Definition As String = GetLocalizedString("REGISTER_Definition")
        'Public Shared REGISTER_End As String = GetLocalizedString("REGISTER_End")
        'Public Shared REGISTER_Module As String = GetLocalizedString("REGISTER_Module")

        Public Shared SECURITY_Installer As String = GetLocalizedString("SECURITY_Installer")
        Public Shared SECURITY_NotRegistered As String = GetLocalizedString("SECURITY_NotRegistered")

        Public Shared SKIN_BeginProcessing As String = GetLocalizedString("SKIN_BeginProcessing")
        Public Shared SKIN_EndProcessing As String = GetLocalizedString("SKIN_EndProcessing")

        Public Shared SQL_Begin As String = GetLocalizedString("SQL_Begin")
        Public Shared SQL_BeginFile As String = GetLocalizedString("SQL_BeginFile")
        Public Shared SQL_BeginUnInstall As String = GetLocalizedString("SQL_BeginUnInstall")
        Public Shared SQL_Committed As String = GetLocalizedString("SQL_Committed")
        Public Shared SQL_End As String = GetLocalizedString("SQL_End")
        Public Shared SQL_EndFile As String = GetLocalizedString("SQL_EndFile")
        Public Shared SQL_EndUnInstall As String = GetLocalizedString("SQL_EndUnInstall")
        Public Shared SQL_Exceptions As String = GetLocalizedString("SQL_Exceptions")
        Public Shared SQL_Executing As String = GetLocalizedString("SQL_Executing")
        Public Shared SQL_RolledBack As String = GetLocalizedString("SQL_RolledBack")
        'Public Shared SQL_UnknownFile As String = GetLocalizedString("SQL_UnknownFile")

        Public Shared UNINSTALL_Start As String = GetLocalizedString("UNINSTALL_Start")
        Public Shared UNINSTALL_StartComp As String = GetLocalizedString("UNINSTALL_StartComp")
        Public Shared UNINSTALL_Failure As String = GetLocalizedString("UNINSTALL_Failure")
        Public Shared UNINSTALL_Success As String = GetLocalizedString("UNINSTALL_Success")
        Public Shared UNINSTALL_SuccessComp As String = GetLocalizedString("UNINSTALL_SuccessComp")

        'Public Shared XML_Loaded As String = GetLocalizedString("XML_Loaded")

#End Region

        Private Shared Function ValidateNode(ByVal propValue As String, ByVal isRequired As Boolean, ByVal log As Logger, ByVal logmessage As String, ByVal defaultValue As String) As String
            If String.IsNullOrEmpty(propValue) Then
                If isRequired Then
                    'Log Error
                    log.AddFailure(logmessage)
                Else
                    'Use Default
                    propValue = defaultValue
                End If
            End If

            Return propValue
        End Function

#Region "Public Shared Methods"

        ''' -----------------------------------------------------------------------------
        ''' <summary>
        ''' The BackupFile method backs up a file to the backup folder
        ''' </summary>
        ''' <param name="installFile">The file to backup</param>
        ''' <param name="basePath">The basePath to the file</param>
        ''' <param name="log">A Logger to log the result</param>
        ''' <history>
        ''' 	[cnurse]	08/03/2007  created
        ''' </history>
        ''' -----------------------------------------------------------------------------
        Public Shared Sub BackupFile(ByVal installFile As InstallFile, ByVal basePath As String, ByVal log As Logger)
            Dim fullFileName As String = Path.Combine(basePath, installFile.FullName)

            ' create the backup folder if neccessary
            If Not Directory.Exists(installFile.BackupPath) Then
                Directory.CreateDirectory(installFile.BackupPath)
            End If

            'Copy file to backup location
            File.Copy(fullFileName, installFile.BackupFileName, True)

            log.AddInfo(Util.FILE_CreateBackup + " - " + installFile.FullName)
        End Sub

        ''' -----------------------------------------------------------------------------
        ''' <summary>
        ''' The RestoreFile method restores a file from the backup folder
        ''' </summary>
        ''' <param name="installFile">The file to restore</param>
        ''' <param name="basePath">The basePath to the file</param>
        ''' <param name="log">A Logger to log the result</param>
        ''' <history>
        ''' 	[cnurse]	08/03/2007  created
        ''' </history>
        ''' -----------------------------------------------------------------------------
        Public Shared Sub RestoreFile(ByVal installFile As InstallFile, ByVal basePath As String, ByVal log As Logger)
            Dim fullFileName As String = Path.Combine(basePath, installFile.FullName)

            'Copy File back over install file
            File.Copy(installFile.BackupFileName, fullFileName, True)

            log.AddInfo(Util.FILE_RestoreBackup + " - " + installFile.FullName)
        End Sub

        ''' -----------------------------------------------------------------------------
        ''' <summary>
        ''' The CopyFile method copies a file from the temporary extract location.
        ''' </summary>
        ''' <param name="installFile">The file to copy</param>
        ''' <param name="basePath">The basePath to the file</param>
        ''' <param name="log">A Logger to log the result</param>
        ''' <history>
        ''' 	[cnurse]	08/03/2007  created
        ''' </history>
        ''' -----------------------------------------------------------------------------
        Public Shared Sub CopyFile(ByVal installFile As InstallFile, ByVal basePath As String, ByVal log As Logger)
            Dim filePath As String = Path.Combine(basePath, installFile.Path)
            Dim fullFileName As String = Path.Combine(basePath, installFile.FullName)

            ' create the folder if neccessary
            If Not Directory.Exists(filePath) Then
                log.AddInfo(Util.FOLDER_Created + " - " + filePath)
                Directory.CreateDirectory(filePath)
            End If

            'Copy file from temp location
            File.Copy(installFile.TempFileName, fullFileName, True)

            log.AddInfo(Util.FILE_Created + installFile.FullName)
        End Sub

        ''' -----------------------------------------------------------------------------
        ''' <summary>
        ''' The DeleteFile method deletes a file.
        ''' </summary>
        ''' <param name="installFile">The file to delete</param>
        ''' <param name="basePath">The basePath to the file</param>
        ''' <param name="log">A Logger to log the result</param>
        ''' <history>
        ''' 	[cnurse]	08/03/2007  created
        ''' </history>
        ''' -----------------------------------------------------------------------------
        Public Shared Sub DeleteFile(ByVal installFile As InstallFile, ByVal basePath As String, ByVal log As Logger)
            Dim fullFileName As String = Path.Combine(basePath, installFile.FullName)

            If File.Exists(fullFileName) Then
                File.Delete(fullFileName)
            End If

            log.AddInfo(Util.FILE_Deleted + " - " + installFile.FullName)
        End Sub

        ''' -----------------------------------------------------------------------------
        ''' <summary>
        ''' The GetLocalizedString method provides a conveniencewrapper around the 
        ''' Localization of Strings
        ''' </summary>
        ''' <param name="key">The localization key</param>
        ''' <returns>The localized string</returns>
        ''' <history>
        ''' 	[cnurse]	07/24/2007  created
        ''' </history>
        ''' -----------------------------------------------------------------------------
        Public Shared Function GetLocalizedString(ByVal key As String) As String
            Return DotNetNuke.Services.Localization.Localization.GetString(key)
        End Function

        ''' -----------------------------------------------------------------------------
        ''' <summary>
        ''' The InstallURL method provides a utility method to build the correct url
        ''' to install a package (and return to where you came from)
        ''' </summary>
        ''' <param name="tabId">The id of the tab you are on</param>
        ''' <param name="type">The type of package you are installing</param>
        ''' <returns>The localized string</returns>
        ''' <history>
        ''' 	[cnurse]	07/26/2007  created
        ''' </history>
        ''' -----------------------------------------------------------------------------
        Public Shared Function InstallURL(ByVal tabId As Integer, ByVal type As String) As String
            Dim params(1) As String
            params(0) = "rtab=" & tabId.ToString()
            If Not String.IsNullOrEmpty(type) Then
                params(1) = "ptype=" & type
            End If

            Return DotNetNuke.Common.Globals.NavigateURL(tabId, "Install", params)
        End Function

        ''' -----------------------------------------------------------------------------
        ''' <summary>
        ''' The UnInstallURL method provides a utility method to build the correct url
        ''' to uninstall a package (and return to where you came from)
        ''' </summary>
        ''' <param name="tabId">The id of the tab you are on</param>
        ''' <param name="packageId">The id of the package you are uninstalling</param>
        ''' <returns>The localized string</returns>
        ''' <history>
        ''' 	[cnurse]	07/31/2007  created
        ''' </history>
        ''' -----------------------------------------------------------------------------
        Public Shared Function UnInstallURL(ByVal tabId As Integer, ByVal packageId As Integer) As String
            Dim params(1) As String
            params(0) = "rtab=" & tabId.ToString()
            params(1) = "packageId=" & packageId.ToString()

            Return DotNetNuke.Common.Globals.NavigateURL(tabId, "UnInstall", params)
        End Function

        Public Shared Function ReadAttribute(ByVal nav As XPathNavigator, ByVal attributeName As String) As String
            Return ValidateNode(nav.GetAttribute(attributeName, ""), False, Nothing, "", "")
        End Function

        Public Shared Function ReadAttribute(ByVal nav As XPathNavigator, ByVal attributeName As String, ByVal log As Logger, ByVal logmessage As String) As String
            Return ValidateNode(nav.GetAttribute(attributeName, ""), True, log, logmessage, "")
        End Function

        Public Shared Function ReadAttribute(ByVal nav As XPathNavigator, ByVal attributeName As String, ByVal isRequired As Boolean, ByVal log As Logger, ByVal logmessage As String, ByVal defaultValue As String) As String
            Return ValidateNode(nav.GetAttribute(attributeName, ""), isRequired, log, logmessage, defaultValue)
        End Function

        Public Shared Function ReadElement(ByVal nav As XPathNavigator, ByVal elementName As String) As String
            Return ValidateNode(XmlUtils.GetNodeValue(nav, elementName), False, Nothing, "", "")
        End Function

        Public Shared Function ReadElement(ByVal nav As XPathNavigator, ByVal elementName As String, ByVal defaultValue As String) As String
            Return ValidateNode(XmlUtils.GetNodeValue(nav, elementName), False, Nothing, "", defaultValue)
        End Function

        Public Shared Function ReadElement(ByVal nav As XPathNavigator, ByVal elementName As String, ByVal log As Logger, ByVal logmessage As String) As String
            Return ValidateNode(XmlUtils.GetNodeValue(nav, elementName), True, log, logmessage, "")
        End Function

        Public Shared Function ReadElement(ByVal nav As XPathNavigator, ByVal elementName As String, ByVal isRequired As Boolean, ByVal log As Logger, ByVal logmessage As String, ByVal defaultValue As String) As String
            Return ValidateNode(XmlUtils.GetNodeValue(nav, elementName), isRequired, log, logmessage, defaultValue)
        End Function


        ''' -----------------------------------------------------------------------------
        ''' <summary>
        ''' The StreamToStream method reads a source stream and wrtites it to a destination stream
        ''' </summary>
        ''' <param name="SourceStream">The Source Stream</param>
        ''' <param name="DestStream">The Destination Stream</param>
        ''' <history>
        ''' 	[cnurse]	08/03/2007  created
        ''' </history>
        ''' -----------------------------------------------------------------------------
        Public Shared Sub StreamToStream(ByVal SourceStream As Stream, ByVal DestStream As Stream)
            Dim buf(1024) As Byte
            Dim count As Integer = 0
            Do
                ' Read the chunk from the source
                count = SourceStream.Read(buf, 0, 1024)

                ' Write the chunk to the destination
                DestStream.Write(buf, 0, count)
            Loop While count > 0
            DestStream.Flush()
        End Sub

        ''' -----------------------------------------------------------------------------
        ''' <summary>
        ''' The WriteStream reads a source stream and writes it to a destination file
        ''' </summary>
        ''' <param name="SourceStream">The Source Stream</param>
        ''' <param name="DestFileName">The Destination file</param>
        ''' <history>
        ''' 	[cnurse]	08/03/2007  created
        ''' </history>
        ''' -----------------------------------------------------------------------------
        Public Shared Sub WriteStream(ByVal SourceStream As Stream, ByVal DestFileName As String)
            ' Create the file
            Dim file As New FileInfo(DestFileName)
            If file.Exists Then
                ' Delete it
                file.Delete()
            End If
            If Not file.Directory.Exists Then
                file.Directory.Create()
            End If

            Dim fileStrm As Stream = file.Create()

            StreamToStream(SourceStream, fileStrm)

            ' Close the stream
            fileStrm.Close()
        End Sub

#End Region

    End Class

End Namespace

