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
Imports DotNetNuke.Services.Localization


Namespace DotNetNuke.Modules.Admin.ResourceInstaller

    ''' -----------------------------------------------------------------------------
    ''' <summary>
    ''' The ResourceInstallerBase class is a Base Class for all Resource Installer
    '''	classes that need to use Localized Strings.  It provides these strings
    '''	as localized Constants.
    ''' </summary>
    ''' <remarks>
    ''' </remarks>
    ''' <history>
    ''' 	[cnurse]	10/29/2004	created
    ''' </history>
    ''' -----------------------------------------------------------------------------
    Public Class ResourceInstallerBase

        'Protected ResourcePortalSettings As PortalSettings = GetPortalSettings()
        Protected DNN_Reading As String = GetLocalizedString("DNN_Reading")
        Protected DNN_Success As String = GetLocalizedString("DNN_Success")
        Protected DNN_Valid As String = GetLocalizedString("DNN_Valid")
        Protected DNN_ValidSkinObject As String = GetLocalizedString("DNN_ValidSkinObject")
        Protected EXCEPTION As String = GetLocalizedString("EXCEPTION")
        Protected EXCEPTION_DesktopSrc As String = GetLocalizedString("EXCEPTION_DesktopSrc")
        Protected EXCEPTION_FileLoad As String = GetLocalizedString("EXCEPTION_FileLoad")
        Protected EXCEPTION_FileName As String = GetLocalizedString("EXCEPTION_FileName")
        Protected EXCEPTION_FileRead As String = GetLocalizedString("EXCEPTION_FileRead")
        Protected EXCEPTION_FolderDesc As String = GetLocalizedString("EXCEPTION_FolderDesc")
        Protected EXCEPTION_FolderName As String = GetLocalizedString("EXCEPTION_FolderName")
        Protected EXCEPTION_FolderProvider As String = GetLocalizedString("EXCEPTION_FolderProvider")
        Protected EXCEPTION_FolderVersion As String = GetLocalizedString("EXCEPTION_FolderVersion")
        Protected EXCEPTION_Format As String = GetLocalizedString("EXCEPTION_LoadFailed")
        Protected EXCEPTION_LoadFailed As String = GetLocalizedString("EXCEPTION_LoadFailed")
        Protected EXCEPTION_MissingDnn As String = GetLocalizedString("EXCEPTION_MissingDnn")
        Protected EXCEPTION_MissingResource As String = GetLocalizedString("EXCEPTION_MissingResource")
        Protected EXCEPTION_MultipleDnn As String = GetLocalizedString("EXCEPTION_MultipleDnn")
        Protected EXCEPTION_Src As String = GetLocalizedString("EXCEPTION_Src")
        Protected EXCEPTION_Type As String = GetLocalizedString("EXCEPTION_Type")
        Protected FILE_Created As String = GetLocalizedString("FILE_Created")
        Protected FILE_Found As String = GetLocalizedString("FILE_Found")
        Protected FILE_Loading As String = GetLocalizedString("FILE_Loading")
        Protected FILE_NotFound As String = GetLocalizedString("FILE_NotFound")
        Protected FILE_ReadSuccess As String = GetLocalizedString("FILE_ReadSuccess")
        Protected FILES_Created As String = GetLocalizedString("FILES_Created")
        Protected FILES_CreatedResources As String = GetLocalizedString("FILES_CreatedResources")
        Protected FILES_Creating As String = GetLocalizedString("FILES_Creating")
        Protected FILES_Expanding As String = GetLocalizedString("FILES_Expanding")
        Protected FILES_Loading As String = GetLocalizedString("FILES_Loading")
        Protected FILES_Reading As String = GetLocalizedString("FILES_Reading")
        Protected FILES_ReadingEnd As String = GetLocalizedString("FILES_ReadingEnd")
        Protected INSTALL_Compatibility As String = GetLocalizedString("INSTALL_Compatibility")
        Protected INSTALL_Dependencies As String = GetLocalizedString("INSTALL_Dependencies")
        Protected INSTALL_Permissions As String = GetLocalizedString("INSTALL_MinimumTrustLevel")
        Protected INSTALL_OlderVersion As String = GetLocalizedString("INSTALL_OlderVersion")
        Protected INSTALL_Start As String = GetLocalizedString("INSTALL_Start")
        Protected INSTALL_Success As String = GetLocalizedString("INSTALL_Success")
        Protected MODULES_ControlInfo As String = GetLocalizedString("MODULES_ControlInfo")
        Protected MODULES_Loading As String = GetLocalizedString("MODULES_Loading")
        Protected REGISTER_Controls As String = GetLocalizedString("REGISTER_Controls")
        Protected REGISTER_Definition As String = GetLocalizedString("REGISTER_Definition")
        Protected REGISTER_End As String = GetLocalizedString("REGISTER_End")
        Protected REGISTER_Module As String = GetLocalizedString("REGISTER_Module")
        Protected SQL_Begin As String = GetLocalizedString("SQL_Begin")
        Protected SQL_BeginFile As String = GetLocalizedString("SQL_BeginFile")
        Protected SQL_End As String = GetLocalizedString("SQL_End")
        Protected SQL_EndFile As String = GetLocalizedString("SQL_EndFile")
        Protected SQL_Exceptions As String = GetLocalizedString("SQL_Exceptions")
        Protected SQL_Executing As String = GetLocalizedString("SQL_Executing")
        Protected SQL_UnknownFile As String = GetLocalizedString("SQL_UnknownFile")
        Protected XML_Loaded As String = GetLocalizedString("XML_Loaded")


#Region "Public Methods"

        Private Function GetLocalizedString(ByVal key As String) As String

            Dim objPortalSettings As PortalSettings = CType(HttpContext.Current.Items("PortalSettings"), PortalSettings)

            If objPortalSettings Is Nothing Then
                Return key
            Else
                Return Localization.GetString(key, objPortalSettings)
            End If

        End Function

#End Region

    End Class

End Namespace

