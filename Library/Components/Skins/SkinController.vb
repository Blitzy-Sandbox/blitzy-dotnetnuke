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
Imports System.Configuration
Imports System.Data
Imports System.IO
Imports ICSharpCode.SharpZipLib.Zip
Imports System.Xml
Imports System.Text.RegularExpressions

Imports DotNetNuke.Entities.Portals
Imports DotNetNuke.Services.Localization

Namespace DotNetNuke.UI.Skins

    ''' -----------------------------------------------------------------------------
    ''' Project	 : DotNetNuke
    ''' Class	 : SkinController
    ''' 
    ''' -----------------------------------------------------------------------------
    ''' <summary>
    '''     Handles the Business Control Layer for Skins
    ''' </summary>
    ''' <remarks>
    ''' </remarks>
    ''' <history>
    ''' 	[willhsc]	3/3/2004	Created
    ''' </history>
    ''' -----------------------------------------------------------------------------
    Public Class SkinController
        ''' <summary>
        ''' Determines if a given skin is defined as a global skin
        ''' </summary>
        ''' <param name="SkinSrc">This is the app relative path and filename of the skin to be checked.</param>
        ''' <returns>True if the skin is located in the HostPath child directories.</returns>
        ''' <remarks>This function performs a quick check to detect the type of skin that is
        ''' passed as a parameter.  Using this method abstracts knowledge of the actual location
        ''' of skins in the file system.
        ''' </remarks>
        ''' <history>
        '''     [Joe Brinkman]	10/20/2007	Created
        ''' </history>
        Public Shared Function IsGlobalSkin(ByVal SkinSrc As String) As Boolean
            Return SkinSrc.Contains(Common.Globals.HostPath)
        End Function

        Public Shared Function FormatSkinPath(ByVal SkinSrc As String) As String
            Dim strSkinSrc As String = SkinSrc

            If strSkinSrc <> "" Then
                strSkinSrc = Left(strSkinSrc, InStrRev(strSkinSrc, "/"))
            End If

            Return strSkinSrc
        End Function

        Public Shared Function FormatSkinSrc(ByVal SkinSrc As String, ByVal PortalSettings As PortalSettings) As String
            Dim strSkinSrc As String = SkinSrc

            If strSkinSrc <> "" Then
                Select Case strSkinSrc.ToLowerInvariant.Substring(0, 3)
                    Case "[g]"
                        strSkinSrc = Regex.Replace(strSkinSrc, "\[g]", Common.Globals.HostPath, RegexOptions.IgnoreCase)
                    Case "[l]"
                        strSkinSrc = Regex.Replace(strSkinSrc, "\[l]", PortalSettings.HomeDirectory, RegexOptions.IgnoreCase)
                End Select
            End If

            Return strSkinSrc
        End Function

        Public Shared Function GetSkin(ByVal SkinRoot As String, ByVal PortalId As Integer, ByVal SkinType As UI.Skins.SkinType) As UI.Skins.SkinInfo
            Dim objSkin As SkinInfo = Nothing
            For Each skin As SkinInfo In GetSkins(PortalId)
                If skin.SkinRoot = SkinRoot And skin.SkinType = SkinType Then
                    objSkin = skin
                    Exit For
                End If
            Next
            Return objSkin
        End Function

        Public Shared Function GetSkins(ByVal PortalId As Integer) As ArrayList
            Dim arrSkins As ArrayList = Nothing

            ' data caching settings
            Dim intCacheTimeout As Integer
            ' calculate the cache settings based on the performance setting
            intCacheTimeout = 20 * Convert.ToInt32(Common.Globals.PerformanceSetting)

            arrSkins = CType(DataCache.GetCache("GetSkins" & PortalId.ToString), ArrayList)
            If arrSkins Is Nothing Then
                arrSkins = CBO.FillCollection(DataProvider.Instance().GetSkins(PortalId), GetType(SkinInfo))

                If intCacheTimeout <> 0 Then
                    DataCache.SetCache("GetSkins" & PortalId.ToString, arrSkins, TimeSpan.FromMinutes(intCacheTimeout), False)
                End If
            End If
            Return arrSkins
        End Function

        Public Shared Sub SetSkin(ByVal SkinRoot As String, ByVal PortalId As Integer, ByVal SkinType As UI.Skins.SkinType, ByVal SkinSrc As String)

            ' remove skin assignment
            DataProvider.Instance().DeleteSkin(SkinRoot, PortalId, SkinType)

            ' add new skin assignment if specified
            If SkinSrc <> "" Then
                DataProvider.Instance().AddSkin(SkinRoot, PortalId, SkinType, SkinSrc)
            End If

            'Clear the Skin Cache
            DataCache.RemoveCache("GetSkins" & PortalId.ToString)
        End Sub

        Public Shared Function UploadSkin(ByVal RootPath As String, ByVal SkinRoot As String, ByVal SkinName As String, ByVal Path As String) As String

            Dim strMessage As String = ""

            Dim objFileStream As FileStream
            objFileStream = New FileStream(Path, FileMode.Open, FileAccess.Read)

            strMessage = UploadSkin(RootPath, SkinRoot, SkinName, CType(objFileStream, Stream))

            objFileStream.Close()

            Return strMessage

        End Function

        Public Shared Function UploadSkin(ByVal RootPath As String, ByVal SkinRoot As String, ByVal SkinName As String, ByVal objInputStream As Stream) As String

            Dim objZipInputStream As New ZipInputStream(objInputStream)

            Dim objZipEntry As ZipEntry
            Dim strExtension As String
            Dim strFileName As String
            Dim objFileStream As FileStream
            Dim intSize As Integer = 2048
            Dim arrData(2048) As Byte
            Dim strMessage As String = ""
            Dim arrSkinFiles As New ArrayList

            'Localized Strings
            Dim ResourcePortalSettings As PortalSettings = GetPortalSettings()
            Dim BEGIN_MESSAGE As String = Localization.GetString("BeginZip", ResourcePortalSettings)
            Dim CREATE_DIR As String = Localization.GetString("CreateDir", ResourcePortalSettings)
            Dim WRITE_FILE As String = Localization.GetString("WriteFile", ResourcePortalSettings)
            Dim FILE_ERROR As String = Localization.GetString("FileError", ResourcePortalSettings)
            Dim END_MESSAGE As String = Localization.GetString("EndZip", ResourcePortalSettings)
            Dim FILE_RESTICTED As String = Localization.GetString("FileRestricted", ResourcePortalSettings)

            strMessage += FormatMessage(BEGIN_MESSAGE, SkinName, -1, False)

            objZipEntry = objZipInputStream.GetNextEntry
            While Not objZipEntry Is Nothing
                If Not objZipEntry.IsDirectory Then
                    ' validate file extension
                    strExtension = objZipEntry.Name.Substring(objZipEntry.Name.LastIndexOf(".") + 1)
                    If InStr(1, ",ASCX,HTM,HTML,CSS,SWF,RESX,XAML,JS," & Common.Globals.HostSettings("FileExtensions").ToString.ToUpper, "," & strExtension.ToUpper) <> 0 Then

                        ' process embedded zip files
                        Select Case objZipEntry.Name.ToLower
                            Case SkinInfo.RootSkin.ToLower & ".zip"
                                Dim objMemoryStream As New MemoryStream
                                intSize = objZipInputStream.Read(arrData, 0, arrData.Length)
                                While intSize > 0
                                    objMemoryStream.Write(arrData, 0, intSize)
                                    intSize = objZipInputStream.Read(arrData, 0, arrData.Length)
                                End While
                                objMemoryStream.Seek(0, SeekOrigin.Begin)
                                strMessage += UploadSkin(RootPath, SkinInfo.RootSkin, SkinName, CType(objMemoryStream, Stream))
                            Case SkinInfo.RootContainer.ToLower & ".zip"
                                Dim objMemoryStream As New MemoryStream
                                intSize = objZipInputStream.Read(arrData, 0, arrData.Length)
                                While intSize > 0
                                    objMemoryStream.Write(arrData, 0, intSize)
                                    intSize = objZipInputStream.Read(arrData, 0, arrData.Length)
                                End While
                                objMemoryStream.Seek(0, SeekOrigin.Begin)
                                strMessage += UploadSkin(RootPath, SkinInfo.RootContainer, SkinName, CType(objMemoryStream, Stream))
                            Case Else
                                strFileName = RootPath & SkinRoot & "\" & SkinName & "\" & objZipEntry.Name

                                ' create the directory if it does not exist
                                If Not Directory.Exists(Path.GetDirectoryName(strFileName)) Then
                                    strMessage += FormatMessage(CREATE_DIR, Path.GetDirectoryName(strFileName), 2, False)
                                    Directory.CreateDirectory(Path.GetDirectoryName(strFileName))
                                End If

                                ' remove the old file
                                If File.Exists(strFileName) Then
                                    File.SetAttributes(strFileName, FileAttributes.Normal)
                                    File.Delete(strFileName)
                                End If
                                ' create the new file
                                objFileStream = File.Create(strFileName)

                                ' unzip the file
                                strMessage += FormatMessage(WRITE_FILE, Path.GetFileName(strFileName), 2, False)
                                intSize = objZipInputStream.Read(arrData, 0, arrData.Length)
                                While intSize > 0
                                    objFileStream.Write(arrData, 0, intSize)
                                    intSize = objZipInputStream.Read(arrData, 0, arrData.Length)
                                End While
                                objFileStream.Close()

                                ' save the skin file
                                Select Case Path.GetExtension(strFileName)
                                    Case ".htm", ".html", ".ascx", ".css"
                                        If strFileName.ToLower.IndexOf(glbAboutPage.ToLower) < 0 Then
                                            arrSkinFiles.Add(strFileName)
                                        End If
                                End Select
                        End Select
                    Else
                        strMessage += FormatMessage(FILE_ERROR, String.Format(FILE_RESTICTED, objZipEntry.Name, Replace(Common.Globals.HostSettings("FileExtensions").ToString, ",", ", *.")), 2, True)
                    End If
                End If
                objZipEntry = objZipInputStream.GetNextEntry
            End While
            strMessage += FormatMessage(END_MESSAGE, SkinName & ".zip", 1, False)
            objZipInputStream.Close()

            ' process the list of skin files
            Dim NewSkin As New UI.Skins.SkinFileProcessor(RootPath, SkinRoot, SkinName)
            strMessage += NewSkin.ProcessList(arrSkinFiles, SkinParser.Portable)

            ' log installation event
            Try
                Dim objEventLogInfo As New Services.Log.EventLog.LogInfo
                objEventLogInfo.LogTypeKey = Services.Log.EventLog.EventLogController.EventLogType.HOST_ALERT.ToString
                objEventLogInfo.LogProperties.Add(New DotNetNuke.Services.Log.EventLog.LogDetailInfo("Install Skin:", SkinName))
                Dim arrMessage As Array = Split(strMessage, "<br>")
                Dim strRow As String
                For Each strRow In arrMessage
                    objEventLogInfo.LogProperties.Add(New DotNetNuke.Services.Log.EventLog.LogDetailInfo("Info:", HtmlUtils.StripTags(strRow, True)))
                Next
                Dim objEventLog As New Services.Log.EventLog.EventLogController
                objEventLog.AddLog(objEventLogInfo)
            Catch ex As Exception
                ' error
            End Try

            Return strMessage

        End Function

        Public Shared Function CanDeleteSkin(ByVal strFolderPath As String, ByVal portalHomeDirMapPath As String) As Boolean
            Dim strSkinType As String
            Dim strSkinFolder As String
            If strFolderPath.ToLower.IndexOf(Common.Globals.HostMapPath.ToLower) <> -1 Then
                strSkinType = "G"
                strSkinFolder = strFolderPath.ToLower.Replace(Common.Globals.HostMapPath.ToLower, "").Replace("\", "/")
            Else
                strSkinType = "L"
                strSkinFolder = strFolderPath.ToLower.Replace(portalHomeDirMapPath.ToLower, "").Replace("\", "/")
            End If

            Return DataProvider.Instance().CanDeleteSkin(strSkinType, strSkinFolder)
        End Function

        Public Shared Function FormatMessage(ByVal Title As String, ByVal Body As String, ByVal Level As Integer, ByVal IsError As Boolean) As String
            Dim Message As String = Title

            If IsError Then
                Message = "<font class=""NormalRed"">" & Title & "</font>"
            End If

            Select Case Level
                Case -1
                    Message = "<hr><br><b>" & Message & "</b>"
                Case 0
                    Message = "<br><br><b>" & Message & "</b>"
                Case 1
                    Message = "<br><b>" & Message & "</b>"
                Case Else
                    Message = "<br><li>" & Message
            End Select

            Return Message & ": " & Body & vbCrLf

        End Function

    End Class

End Namespace