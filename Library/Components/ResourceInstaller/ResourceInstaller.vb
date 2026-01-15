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
Imports System.Threading
Imports System.IO
Imports System.Xml
Imports DotNetNuke.UI.Skins
Imports DotNetNuke.Services.Localization
Imports DotNetNuke.Services.Packages

Namespace DotNetNuke.Modules.Admin.ResourceInstaller

    Public Class ResourceInstaller

#Region "Private Shared Methods"

        Private Shared Sub DeleteFile(ByVal strFile As String)
            ' delete the file
            Try
                File.SetAttributes(strFile, FileAttributes.Normal)
                File.Delete(strFile)
            Catch
                ' error removing the file
            End Try
        End Sub

#End Region

#Region "Public Shared Methods"

#End Region

        ''' -----------------------------------------------------------------------------
        ''' <summary>
        ''' Install installs resources that are located in the appropriate Install folder
        ''' </summary>
        ''' <remarks> This overload install all resources with no feedback</remarks>
        ''' <history>
        ''' 	[cnurse]	02/19/2007	updated to support new Wizard
        ''' </history>
        ''' -----------------------------------------------------------------------------
        Public Shared Sub Install()
            Install(False, 0, "")
        End Sub

        ''' -----------------------------------------------------------------------------
        ''' <summary>
        ''' Install installs resources that are located in the appropriate Install folder
        ''' </summary>
        ''' <remarks>This overload install all resources with optional feedback</remarks>
        '''	<param name="writeFeedback">A flag indicating whether to output feeedback to the Response</param>
        ''' <param name="indent">The indentation to use for the feedback</param>
        ''' <history>
        ''' 	[cnurse]	02/19/2007	updated to support new Wizard
        ''' </history>
        ''' -----------------------------------------------------------------------------
        Public Shared Sub Install(ByVal writeFeedback As Boolean, ByVal indent As Int32)
            Install(writeFeedback, indent, "")
        End Sub

        ''' -----------------------------------------------------------------------------
        ''' <summary>
        ''' Install install resources that are located in the appropriate Install folder
        ''' </summary>
        ''' <remarks>
        ''' </remarks>
        '''	<param name="writeFeedback">A flag indicating whether to output feeedback to the Response</param>
        ''' <param name="indent">The indentation to use for the feedback</param>
        '''	<param name="type">The type of resources to install to install ("" = all)</param>
        ''' <history>
        ''' 	[cnurse]	02/19/2007	updated to support new Wizard
        ''' </history>
        ''' -----------------------------------------------------------------------------
        Public Shared Sub Install(ByVal writeFeedback As Boolean, ByVal indent As Int32, ByVal type As String)

            Dim arrFolders As String()
            Dim strFolder As String
            Dim arrFiles As String()
            Dim strFile As String

            Dim InstallPath As String = ApplicationMapPath & "\Install"

            If Directory.Exists(InstallPath) Then
                arrFolders = Directory.GetDirectories(InstallPath)
                For Each strFolder In arrFolders
                    arrFiles = Directory.GetFiles(strFolder)
                    For Each strFile In arrFiles
                        Select Case type.ToLower
                            Case "modules"
                                ' install custom module
                                If strFile.ToLower.IndexOf("\module\") <> -1 Then
                                    InstallModule(strFile, False, writeFeedback, indent)
                                End If
                            Case Else
                                ' install custom module
                                If strFile.ToLower.IndexOf("\module\") <> -1 Then
                                    InstallModule(strFile, False, writeFeedback, indent)
                                End If

                                ' install skin
                                If strFile.ToLower.IndexOf("\skin\") <> -1 Then
                                    InstallSkin(strFile, False, writeFeedback, indent)
                                End If

                                ' install container
                                If strFile.ToLower.IndexOf("\container\") <> -1 Then
                                    InstallContainer(strFile, False, writeFeedback, indent)
                                End If

                                ' install language pack
                                If strFile.ToLower.IndexOf("\language\") <> -1 Then
                                    InstallLanguage(strFile, False, writeFeedback, indent)
                                End If

                                ' install package
                                If strFile.ToLower.IndexOf("\package\") <> -1 Then
                                    InstallPackage(strFile, False, writeFeedback, indent)
                                End If

                                ' install template
                                InstallTemplate(strFile, writeFeedback, indent)

                                'Install Portal(s)
                                InstallPortal(strFile, writeFeedback, indent)
                        End Select 'type

                    Next 'strFile In arrFiles
                Next 'strFolder In arrFolders
            End If 'Directory.Exists(InstallPath)

        End Sub

        ''' -----------------------------------------------------------------------------
        ''' <summary>
        ''' InstallContainer installs a single container
        ''' </summary>
        ''' <remarks>
        ''' </remarks>
        '''	<param name="strFile">The container file to install</param>
        ''' <param name="allowResources">A flag that allows packages with the extension .resources to be installed</param>
        '''	<param name="writeFeedback">A flag indicating whether to output feeedback to the Response</param>
        ''' <param name="indent">The indentation to use for the feedback</param>
        ''' <history>
        ''' 	[cnurse]	02/19/2007	extracted to new method to support new Wizard
        ''' </history>
        ''' -----------------------------------------------------------------------------
        Public Shared Function InstallContainer(ByVal strFile As String, ByVal allowResources As Boolean, ByVal writeFeedback As Boolean, ByVal indent As Integer) As String
            Dim errorMessage As String = Null.NullString

            ' check if valid container
            If Path.GetExtension(strFile.ToLower) = ".zip" OrElse (allowResources AndAlso Path.GetExtension(strFile.ToLower) = ".resources") Then
                If writeFeedback Then
                    HtmlUtils.WriteFeedback(HttpContext.Current.Response, indent, "Installing Container File " & strFile & ":<br>")
                End If
                errorMessage = SkinController.UploadSkin(Common.Globals.HostMapPath, SkinInfo.RootContainer, Path.GetFileNameWithoutExtension(strFile), strFile)
                ' delete file
                DeleteFile(strFile)
            End If

            Return errorMessage
        End Function

        ''' -----------------------------------------------------------------------------
        ''' <summary>
        ''' InstallLanguage installs a single language
        ''' </summary>
        ''' <remarks>
        ''' </remarks>
        '''	<param name="strFile">The language file to install</param>
        ''' <param name="allowResources">A flag that allows packages with the extension .resources to be installed</param>
        '''	<param name="writeFeedback">A flag indicating whether to output feeedback to the Response</param>
        ''' <param name="indent">The indentation to use for the feedback</param>
        ''' <history>
        ''' 	[cnurse]	02/19/2007	extracted to new method to support new Wizard
        ''' </history>
        ''' -----------------------------------------------------------------------------
        Public Shared Sub InstallLanguage(ByVal strFile As String, ByVal allowResources As Boolean, ByVal writeFeedback As Boolean, ByVal indent As Integer)
            ' check if valid language pack
            If Path.GetExtension(strFile.ToLower) = ".zip" OrElse (allowResources AndAlso Path.GetExtension(strFile.ToLower) = ".resources") Then
                If writeFeedback Then
                    HtmlUtils.WriteFeedback(HttpContext.Current.Response, indent, "Installing Language File " & strFile & ":<br>")
                End If
                Dim objLocaleFilePackReader As New LocaleFilePackReader
                objLocaleFilePackReader.Install(strFile)
                ' delete file
                DeleteFile(strFile)
            End If
        End Sub

        ''' -----------------------------------------------------------------------------
        ''' <summary>
        ''' InstallModule installs a single module
        ''' </summary>
        ''' <remarks>
        ''' </remarks>
        '''	<param name="strFile">The file to install</param>
        ''' <param name="allowResources">A flag that allows packages with the extension .resources to be installed</param>
        '''	<param name="writeFeedback">A flag indicating whether to output feeedback to the Response</param>
        ''' <param name="indent">The indentation to use for the feedback</param>
        ''' <history>
        ''' 	[cnurse]	02/19/2007	upgraded to support new Wizard and converted into a 
        '''                             public static/shared function
        ''' </history>
        ''' -----------------------------------------------------------------------------
        Public Shared Function InstallModule(ByVal strFile As String, ByVal allowResources As Boolean, ByVal writeFeedback As Boolean, ByVal indent As Integer) As Boolean
            Dim blnSuccess As Boolean = Null.NullBoolean

            ' check if valid custom module
            If Path.GetExtension(strFile.ToLower) = ".zip" OrElse (allowResources AndAlso Path.GetExtension(strFile.ToLower) = ".resources") Then
                If writeFeedback Then
                    HtmlUtils.WriteFeedback(HttpContext.Current.Response, indent, "Installing Module File " & Path.GetFileNameWithoutExtension(strFile) & ": ")
                End If
                Dim objPaInstaller As New PaInstaller(strFile, Common.Globals.ApplicationMapPath)
                blnSuccess = objPaInstaller.Install()
                If writeFeedback Then
                    HtmlUtils.WriteSuccessError(HttpContext.Current.Response, blnSuccess)
                End If
                ' delete file (also when error on installing)
                DeleteFile(strFile)

                Return blnSuccess
            End If
        End Function

        ''' -----------------------------------------------------------------------------
        ''' <summary>
        ''' InstallPackage installs a single package
        ''' </summary>
        ''' <remarks>
        ''' </remarks>
        '''	<param name="strFile">The file to install</param>
        ''' <param name="allowResources">A flag that allows packages with the extension .resources to be installed</param>
        '''	<param name="writeFeedback">A flag indicating whether to output feeedback to the Response</param>
        ''' <param name="indent">The indentation to use for the feedback</param>
        ''' <history>
        ''' 	[cnurse]	08/16/2007	created
        ''' </history>
        ''' -----------------------------------------------------------------------------
        Public Shared Function InstallPackage(ByVal strFile As String, ByVal allowResources As Boolean, ByVal writeFeedback As Boolean, ByVal indent As Integer) As Boolean
            Dim blnSuccess As Boolean = Null.NullBoolean

            ' check if valid custom module
            If Path.GetExtension(strFile.ToLower) = ".zip" OrElse (allowResources AndAlso Path.GetExtension(strFile.ToLower) = ".resources") Then
                If writeFeedback Then
                    HtmlUtils.WriteFeedback(HttpContext.Current.Response, indent, "Installing Package File " & Path.GetFileNameWithoutExtension(strFile) & ": ")
                End If
                Dim objInstaller As New Installer(New FileStream(strFile, FileMode.Open, FileAccess.Read), Common.Globals.ApplicationMapPath)
                objInstaller.InstallerInfo.ReadManifest()
                blnSuccess = objInstaller.Install()
                If writeFeedback Then
                    HtmlUtils.WriteSuccessError(HttpContext.Current.Response, blnSuccess)
                End If
                ' delete file (also when error on installing)
                DeleteFile(strFile)

                Return blnSuccess
            End If
        End Function

        ''' -----------------------------------------------------------------------------
        ''' <summary>
        ''' InstallPortal installs a single portal
        ''' </summary>
        ''' <remarks>
        ''' </remarks>
        '''	<param name="strFile">The portal file to install</param>
        '''	<param name="writeFeedback">A flag indicating whether to output feeedback to the Response</param>
        ''' <param name="indent">The indentation to use for the feedback</param>
        ''' <history>
        ''' 	[cnurse]	02/19/2007	upgraded to support new Wizard and converted into a 
        '''                             public static/shared function
        ''' </history>
        ''' -----------------------------------------------------------------------------
        Public Shared Function InstallPortal(ByVal strFile As String, ByVal writeFeedback As Boolean, ByVal indent As Integer) As String
            Dim errorMessage As String = Null.NullString

            If strFile.ToLower.IndexOf("\portal\") <> -1 Then
                'Check if valid portals file
                If strFile.ToLower.IndexOf(".resources") <> -1 Then
                    Dim xmlDoc As New XmlDocument
                    Dim node As XmlNode
                    Dim nodes As XmlNodeList
                    Dim intPortalId As Integer
                    xmlDoc.Load(strFile)

                    ' parse portal(s) if available
                    nodes = xmlDoc.SelectNodes("//dotnetnuke/portals/portal")
                    For Each node In nodes
                        If Not node Is Nothing Then
                            If writeFeedback Then
                                HtmlUtils.WriteFeedback(HttpContext.Current.Response, indent, "Installing Portals:<br>")
                            End If
                            intPortalId = Services.Upgrade.Upgrade.AddPortal(node, True, indent)
                            If writeFeedback Then
                                If intPortalId > -1 Then
                                    HtmlUtils.WriteFeedback(HttpContext.Current.Response, indent + 2, "Successfully Installed Portal " & intPortalId & ":<br>")
                                Else
                                    HtmlUtils.WriteFeedback(HttpContext.Current.Response, indent + 2, "Portal failed to install:<br>")
                                End If
                            End If
                        End If
                    Next
                    ' delete file
                    DeleteFile(strFile)
                End If
            End If

            Return errorMessage
        End Function

        ''' -----------------------------------------------------------------------------
        ''' <summary>
        ''' InstallSkin installs a single skin
        ''' </summary>
        ''' <remarks>
        ''' </remarks>
        '''	<param name="strFile">The skin file to install</param>
        ''' <param name="allowResources">A flag that allows packages with the extension .resources to be installed</param>
        '''	<param name="writeFeedback">A flag indicating whether to output feeedback to the Response</param>
        ''' <param name="indent">The indentation to use for the feedback</param>
        ''' <history>
        ''' 	[cnurse]	02/19/2007	extracted to new method to support new Wizard
        ''' </history>
        ''' -----------------------------------------------------------------------------
        Public Shared Function InstallSkin(ByVal strFile As String, ByVal allowResources As Boolean, ByVal writeFeedback As Boolean, ByVal indent As Integer) As String
            Dim errorMessage As String = Null.NullString

            ' check if valid skin
            If Path.GetExtension(strFile.ToLower) = ".zip" OrElse (allowResources AndAlso Path.GetExtension(strFile.ToLower) = ".resources") Then
                If writeFeedback Then
                    HtmlUtils.WriteFeedback(HttpContext.Current.Response, indent, "Installing Skin File " & strFile & ":<br>")
                End If
                errorMessage = SkinController.UploadSkin(Common.Globals.HostMapPath, SkinInfo.RootSkin, Path.GetFileNameWithoutExtension(strFile), strFile)
                ' delete file
                DeleteFile(strFile)
            End If

            Return errorMessage
        End Function

        ''' -----------------------------------------------------------------------------
        ''' <summary>
        ''' InstallTemplate installs a single potal template
        ''' </summary>
        ''' <remarks>
        ''' </remarks>
        '''	<param name="strFile">The file to install</param>
        '''	<param name="writeFeedback">A flag indicating whether to output feeedback to the Response</param>
        ''' <param name="indent">The indentation to use for the feedback</param>
        ''' <history>
        ''' 	[cnurse]	02/19/2007	upgraded to support new Wizard and converted into a 
        '''                             public static/shared function
        ''' </history>
        ''' -----------------------------------------------------------------------------
        Public Shared Function InstallTemplate(ByVal strFile As String, ByVal writeFeedback As Boolean, ByVal indent As Integer) As String
            Dim errorMessage As String = Null.NullString

            If strFile.ToLower.IndexOf("\template\") <> -1 Then
                ' check if valid template file ( .template or .template.resources )
                If strFile.ToLower.IndexOf(".template") <> -1 Then
                    If writeFeedback Then
                        HtmlUtils.WriteFeedback(HttpContext.Current.Response, indent, "Installing Template " & strFile & ":<br>")
                    End If
                    Dim strNewFile As String = Common.Globals.HostMapPath & "\" & Path.GetFileName(strFile)
                    If File.Exists(strNewFile) Then
                        File.Delete(strNewFile)
                    End If
                    File.Move(strFile, strNewFile)
                End If
            End If

            Return errorMessage
        End Function

    End Class

End Namespace
