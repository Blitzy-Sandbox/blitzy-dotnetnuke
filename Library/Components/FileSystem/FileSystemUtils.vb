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
Imports System.Collections.Generic
Imports System.Web.Caching
Imports DotNetNuke.Services.FileSystem
Imports DotNetNuke.Services.Localization
Imports DotNetNuke.Security.Permissions
Imports ICSharpCode.SharpZipLib.Checksums
Imports ICSharpCode.SharpZipLib.Zip

Namespace DotNetNuke.Common.Utilities

    Public Class FileSystemUtils

#Region "Private Methods"

        ''' -----------------------------------------------------------------------------
        ''' <summary>
        ''' Adds a File
        ''' </summary>
        ''' <param name="strFile">The File Name</param>
        ''' <param name="PortalId">The Id of the Portal</param>
        ''' <param name="ClearCache">A flag that indicates whether the file cache should be cleared</param>
        ''' <remarks>This method is called by the SynchonizeFolder method, when the file exists in the file system
        ''' but not in the Database
        ''' </remarks>
        ''' <history>
        ''' 	[cnurse]	12/2/2004	Created
        '''     [cnurse]    04/26/2006  Updated to account for secure storage
        ''' </history>
        ''' -----------------------------------------------------------------------------
        Private Shared Function AddFile(ByVal strFile As String, ByVal PortalId As Integer, ByVal ClearCache As Boolean, ByVal folder As FolderInfo) As String

            Dim retValue As String = ""
            Try
                Dim objFileController As New DotNetNuke.Services.FileSystem.FileController
                Dim fInfo As System.IO.FileInfo = New System.IO.FileInfo(strFile)
                Dim sourceFolderName As String = GetSubFolderPath(strFile, PortalId)

                Dim sourceFileName As String
                'Remove the resources extesnion if we are in a Secure Folder
                If folder.StorageLocation = FolderController.StorageLocationTypes.SecureFileSystem Then
                    sourceFileName = GetFileName(strFile)
                Else
                    sourceFileName = strFile
                End If

                Dim file As DotNetNuke.Services.FileSystem.FileInfo = objFileController.GetFile(sourceFileName, PortalId, folder.FolderID)

                If file Is Nothing Then
                    'Add the new File
                    AddFile(PortalId, fInfo.OpenRead(), strFile, "", fInfo.Length, sourceFolderName, True, ClearCache, True)
                Else
                    If file.Size <> fInfo.Length Then
                        'optimistic assumption for speed: update only if filesize has changed
                        Dim extension As String = Path.GetExtension(strFile).Replace(".", "")
                        UpdateFileData(file.FileId, folder.FolderID, PortalId, sourceFileName, extension, GetContentType(extension), fInfo.Length, sourceFolderName)
                    End If
                End If
            Catch ex As Exception
                retValue = ex.Message
            End Try

            Return retValue
        End Function

        Private Shared Function AddFile(ByVal PortalId As Integer, ByVal inStream As Stream, ByVal fileName As String, ByVal contentType As String, ByVal length As Long, ByVal folderName As String, ByVal closeInputStream As Boolean, ByVal clearCache As Boolean) As String
            Return AddFile(PortalId, inStream, fileName, contentType, length, folderName, closeInputStream, clearCache, False)
        End Function

        ''' -----------------------------------------------------------------------------
        ''' <summary>
        ''' Adds a File
        ''' </summary>
        ''' <param name="PortalId">The Id of the Portal</param>
        ''' <param name="inStream">The stream to add</param>
        ''' <param name="contentType">The type of the content</param>
        ''' <param name="length">The length of the content</param>
        ''' <param name="folderName">The name of the folder</param>
        ''' <param name="closeInputStream">A flag that dermines if the Input Stream should be closed.</param>
        ''' <param name="ClearCache">A flag that indicates whether the file cache should be cleared</param>
        ''' <remarks>This method adds a new file
        ''' </remarks>
        ''' <history>
        '''     [cnurse]    04/26/2006  Created
        ''' </history>
        ''' -----------------------------------------------------------------------------
        Private Shared Function AddFile(ByVal PortalId As Integer, ByVal inStream As Stream, ByVal fileName As String, ByVal contentType As String, ByVal length As Long, ByVal folderName As String, ByVal closeInputStream As Boolean, ByVal clearCache As Boolean, ByVal synchronize As Boolean) As String

            Dim objFolderController As New DotNetNuke.Services.FileSystem.FolderController
            Dim objFileController As New DotNetNuke.Services.FileSystem.FileController
            Dim sourceFolderName As String = GetSubFolderPath(fileName, PortalId)
            Dim folder As FolderInfo = objFolderController.GetFolder(PortalId, sourceFolderName, False)
            Dim sourceFileName As String = GetFileName(fileName)
            Dim intFileID As Integer
            Dim retValue As String = ""

            retValue += CheckValidFileName(fileName)
            If retValue.Length > 0 Then
                Return retValue
            End If

            Dim extension As String = Path.GetExtension(fileName).Replace(".", "")
            If contentType = "" Then
                contentType = GetContentType(extension)
            End If

            'Add file to Database
            intFileID = objFileController.AddFile(PortalId, sourceFileName, extension, length, Null.NullInteger, Null.NullInteger, contentType, folderName, folder.FolderID, clearCache)

            'Save file to File Storage
            If folder.StorageLocation <> FolderController.StorageLocationTypes.InsecureFileSystem Or synchronize = False Then
                WriteStream(intFileID, inStream, fileName, folder.StorageLocation, closeInputStream)
            End If

            'Update the FileData
            retValue += UpdateFileData(intFileID, folder.FolderID, PortalId, sourceFileName, extension, contentType, length, folderName)

            If folder.StorageLocation <> FolderController.StorageLocationTypes.InsecureFileSystem Then
                'try and delete the Insecure file
                DeleteFile(fileName)
            End If

            If folder.StorageLocation <> FolderController.StorageLocationTypes.SecureFileSystem Then
                'try and delete the Secure file
                DeleteFile(fileName & glbProtectedExtension)
            End If

            Return retValue

        End Function

        ''' -----------------------------------------------------------------------------
        ''' <summary>
        ''' Adds a Folder
        ''' </summary>
        ''' <param name="PortalId">The Id of the Portal</param>
        ''' <param name="relativePath">The relative path of the folder</param>
        ''' <param name="StorageLocation">The type of storage location</param>
        ''' <history>
        '''     [cnurse]    04/26/2006  Created
        ''' </history>
        ''' -----------------------------------------------------------------------------
        Private Shared Function AddFolder(ByVal PortalId As Integer, ByVal relativePath As String, ByVal StorageLocation As Integer) As Integer

            Dim objFolderController As New DotNetNuke.Services.FileSystem.FolderController
            Dim isProtected As Boolean = FileSystemUtils.DefaultProtectedFolders(relativePath)
            Dim FolderID As Integer = objFolderController.AddFolder(PortalId, relativePath, StorageLocation, isProtected, False)

            If PortalId <> Null.NullInteger Then
                'Set Folder Permissions to inherit from parent
                SetFolderPermissions(PortalId, FolderID, relativePath)
            End If

            Return FolderID

        End Function

        ''' -----------------------------------------------------------------------------
        ''' <summary>
        ''' Checks that the file name is valid
        ''' </summary>
        ''' <param name="strFileName">The name of the file</param>
        ''' <history>
        '''     [cnurse]    04/26/2006  Created
        ''' </history>
        ''' -----------------------------------------------------------------------------
        Private Shared Function CheckValidFileName(ByVal strFileName As String) As String

            Dim retValue As String = Null.NullString

            ' filenames can not contain "'"
            If strFileName.IndexOf("'") > -1 Then
                If Not HttpContext.Current Is Nothing Then
                    retValue = Localization.GetString("InvalidFileName")
                Else
                    retValue = "InvalidFileName"
                End If
            End If

            ' check if file extension is restricted
            Dim strExtension As String = Path.GetExtension(strFileName).Replace(".", "")
            Dim strWhiteList As String = DotNetNuke.Entities.Host.HostSettings.GetHostSetting("FileExtensions").ToLower
            If String.IsNullOrEmpty(strExtension) OrElse ("," & strWhiteList & ",").IndexOf("," & strExtension.ToLower & ",") = -1 Then
                If Not HttpContext.Current Is Nothing Then
                    retValue = "<br>" & String.Format(Localization.GetString("RestrictedFileType"), strFileName, Replace(strWhiteList, ",", ", *."))
                Else
                    retValue = "RestrictedFileType"
                End If
            End If

            'Return
            Return retValue

        End Function

        ''' -----------------------------------------------------------------------------
        ''' <summary>
        ''' Gets the filename for a file path
        ''' </summary>
        ''' <param name="filePath">The full name of the file</param>
        ''' <history>
        '''     [cnurse]    04/26/2006  Created
        ''' </history>
        ''' -----------------------------------------------------------------------------
        Private Shared Function GetFileName(ByVal filePath As String) As String
            Return System.IO.Path.GetFileName(filePath).Replace(glbProtectedExtension, "")
        End Function

        Private Shared Sub RemoveOrphanedFiles(ByVal folder As FolderInfo, ByVal PortalId As Integer)
            Dim objFile As DotNetNuke.Services.FileSystem.FileInfo
            Dim objFileController As New FileController

            If folder.StorageLocation <> FolderController.StorageLocationTypes.DatabaseSecure Then
                For Each objFile In GetFilesByFolder(PortalId, folder.FolderID)
                    RemoveOrphanedFile(objFile, PortalId)
                Next
            End If
        End Sub

        Private Shared Sub RemoveOrphanedFile(ByVal objFile As DotNetNuke.Services.FileSystem.FileInfo, ByVal PortalId As Integer)
            Dim objFileController As New FileController

            Dim strFile As String = ""
            Select Case objFile.StorageLocation
                Case FolderController.StorageLocationTypes.InsecureFileSystem
                    strFile = objFile.PhysicalPath
                Case FolderController.StorageLocationTypes.SecureFileSystem
                    strFile = objFile.PhysicalPath & glbProtectedExtension
            End Select

            If strFile <> "" Then
                If Not File.Exists(strFile) Then
                    objFileController.DeleteFile(PortalId, objFile.FileName, objFile.FolderId, True)
                End If
            End If
        End Sub

        ''' -----------------------------------------------------------------------------
        ''' <summary>
        ''' Updates a File
        ''' </summary>
        ''' <param name="strSourceFile">The original File Name</param>
        ''' <param name="strDestFile">The new File Name</param>
        ''' <param name="isCopy">Flag determines whether file is to be be moved or copied</param>
        ''' <remarks>
        ''' </remarks>
        ''' <history>
        ''' 	[cnurse]	12/2/2004	Created
        ''' </history>
        ''' -----------------------------------------------------------------------------
        Private Shared Function UpdateFile(ByVal strSourceFile As String, ByVal strDestFile As String, ByVal PortalId As Integer, ByVal isCopy As Boolean, ByVal isNew As Boolean, ByVal ClearCache As Boolean) As String

            Dim retValue As String = ""
            retValue += CheckValidFileName(strSourceFile) & " "
            retValue += CheckValidFileName(strDestFile)
            If retValue.Length > 1 Then
                Return retValue
            End If
            retValue = ""

            Try
                Dim objFolderController As New DotNetNuke.Services.FileSystem.FolderController
                Dim objFileController As New DotNetNuke.Services.FileSystem.FileController
                Dim sourceFolderName As String = GetSubFolderPath(strSourceFile, PortalId)
                Dim sourceFileName As String = GetFileName(strSourceFile)
                Dim sourceFolder As FolderInfo = objFolderController.GetFolder(PortalId, sourceFolderName, False)

                Dim destFileName As String = GetFileName(strDestFile)
                Dim destFolderName As String = GetSubFolderPath(strDestFile, PortalId)

                Dim file As DotNetNuke.Services.FileSystem.FileInfo
                If Not sourceFolder Is Nothing Then

                    file = objFileController.GetFile(sourceFileName, PortalId, sourceFolder.FolderID)
                    If Not file Is Nothing Then
                        'Get the source Content from wherever it is
                        Dim sourceStream As Stream = CType(GetFileStream(file), Stream)

                        If isCopy Then
                            'Add the new File
                            AddFile(PortalId, sourceStream, strDestFile, "", file.Size, destFolderName, True, ClearCache)
                        Else
                            'Move/Update existing file
                            Dim destinationFolder As FolderInfo = objFolderController.GetFolder(PortalId, destFolderName, False)

                            'Now move the file
                            If Not destinationFolder Is Nothing Then

                                objFileController.UpdateFile(file.FileId, destFileName, file.Extension, file.Size, file.Width, file.Height, file.ContentType, destFolderName, destinationFolder.FolderID)

                                'Write the content to the Destination
                                WriteStream(file.FileId, sourceStream, strDestFile, destinationFolder.StorageLocation, True)

                                'Now we need to clean up the original files
                                If sourceFolder.StorageLocation = FolderController.StorageLocationTypes.InsecureFileSystem Then
                                    'try and delete the Insecure file
                                    DeleteFile(strSourceFile)
                                End If
                                If sourceFolder.StorageLocation = FolderController.StorageLocationTypes.SecureFileSystem Then
                                    'try and delete the Secure file
                                    DeleteFile(strSourceFile & glbProtectedExtension)
                                End If
                            End If
                        End If
                    End If
                End If

            Catch ex As Exception
                retValue = ex.Message
            End Try

            Return retValue

        End Function

        Private Shared Function UpdateFileData(ByVal fileID As Integer, ByVal folderID As Integer, ByVal PortalId As Integer, ByVal fileName As String, ByVal extension As String, ByVal contentType As String, ByVal length As Long, ByVal folderName As String) As String
            Dim retvalue As String = ""
            Try
                Dim objFileController As New DotNetNuke.Services.FileSystem.FileController
                Dim imgImage As System.Drawing.Image
                Dim imageWidth As Integer
                Dim imageHeight As Integer

                If Convert.ToBoolean(InStr(1, glbImageFileTypes & ",", extension.ToLower & ",")) Then
                    Try
                        Dim objFile As DotNetNuke.Services.FileSystem.FileInfo = objFileController.GetFileById(fileID, PortalId)
                        Dim imageStream As Stream = GetFileStream(objFile)
                        imgImage = Drawing.Image.FromStream(imageStream)
                        imageHeight = imgImage.Height
                        imageWidth = imgImage.Width
                        imgImage.Dispose()
                        imageStream.Close()
                    Catch
                        ' error loading image file
                        contentType = "application/octet-stream"
                    Finally
                        'Update the File info
                        objFileController.UpdateFile(fileID, fileName, extension, length, imageWidth, imageHeight, contentType, folderName, folderID)
                    End Try
                End If
            Catch ex As Exception
                retvalue = ex.Message
            End Try

            Return retvalue
        End Function

        ''' -----------------------------------------------------------------------------
        ''' <summary>
        ''' Writes a Stream to the appropriate File Storage
        ''' </summary>
        ''' <param name="fileId">The Id of the File</param>
        ''' <param name="inStream">The Input Stream</param>
        ''' <param name="fileName">The name of the file</param>
        ''' <param name="StorageLocation">The type of storage location</param>
        ''' <param name="closeInputStream">A flag that dermines if the Input Stream should be closed.</param>
        ''' <remarks>
        ''' </remarks>
        ''' <history>
        ''' 	[cnurse]	04/27/2006	Created
        ''' </history>
        ''' -----------------------------------------------------------------------------
        Private Shared Sub WriteStream(ByVal fileId As Integer, ByVal inStream As Stream, ByVal fileName As String, ByVal storageLocation As Integer, ByVal closeInputStream As Boolean)

            Dim objFileController As New DotNetNuke.Services.FileSystem.FileController

            ' Buffer to read 2K bytes in chunk:
            Dim arrData(2048) As Byte
            Dim outStream As Stream = Nothing
            Select Case storageLocation
                Case FolderController.StorageLocationTypes.DatabaseSecure
                    objFileController.ClearFileContent(fileId)
                    outStream = New MemoryStream
                Case FolderController.StorageLocationTypes.SecureFileSystem
                    If File.Exists(fileName & glbProtectedExtension) = True Then
                        File.Delete(fileName & glbProtectedExtension)
                    End If
                    outStream = New FileStream(fileName & glbProtectedExtension, FileMode.Create)
                Case FolderController.StorageLocationTypes.InsecureFileSystem
                    If File.Exists(fileName) = True Then
                        File.Delete(fileName)
                    End If
                    outStream = New FileStream(fileName, FileMode.Create)
            End Select

            Try
                ' Total bytes to read:
                Dim intLength As Integer
                ' Read the data in buffer
                intLength = inStream.Read(arrData, 0, arrData.Length)
                While intLength > 0
                    ' Write the data to the current output stream.
                    outStream.Write(arrData, 0, intLength)

                    'Read the next chunk
                    intLength = inStream.Read(arrData, 0, arrData.Length)
                End While

                If storageLocation = FolderController.StorageLocationTypes.DatabaseSecure Then
                    outStream.Seek(0, SeekOrigin.Begin)
                    objFileController.UpdateFileContent(fileId, outStream)
                End If
            Catch ex As Exception
            Finally
                If IsNothing(inStream) = False And closeInputStream Then
                    ' Close the file.
                    inStream.Close()
                End If
                If IsNothing(outStream) = False Then
                    ' Close the file.
                    outStream.Close()
                End If
            End Try

        End Sub

        Private Shared Sub WriteStream(ByVal objResponse As HttpResponse, ByVal objStream As Stream)
            ' Buffer to read 10K bytes in chunk:
            Dim bytBuffer(10000) As Byte

            ' Length of the file:
            Dim intLength As Integer

            ' Total bytes to read:
            Dim lngDataToRead As Long

            Try
                ' Total bytes to read:
                lngDataToRead = objStream.Length

                'objResponse.ContentType = "application/octet-stream"

                ' Read the bytes.
                While lngDataToRead > 0
                    ' Verify that the client is connected.
                    If objResponse.IsClientConnected Then
                        ' Read the data in buffer
                        intLength = objStream.Read(bytBuffer, 0, 10000)

                        ' Write the data to the current output stream.
                        objResponse.OutputStream.Write(bytBuffer, 0, intLength)

                        ' Flush the data to the HTML output.
                        objResponse.Flush()

                        ReDim bytBuffer(10000)       ' Clear the buffer
                        lngDataToRead = lngDataToRead - intLength
                    Else
                        'prevent infinite loop if user disconnects
                        lngDataToRead = -1
                    End If
                End While

            Catch ex As Exception
                ' Trap the error, if any.
                objResponse.Write("Error : " & ex.Message)
            Finally
                If IsNothing(objStream) = False Then
                    ' Close the file.
                    objStream.Close()
                End If
            End Try
        End Sub

#End Region

#Region "Path Manipulation Methods"

        Public Shared Function AddTrailingSlash(ByVal strSource As String) As String
            If Not strSource.EndsWith("\") Then strSource = strSource & "\"
            Return strSource
        End Function

        Public Shared Function RemoveTrailingSlash(ByVal strSource As String) As String
            If strSource = "" Then Return ""
            If Mid(strSource, Len(strSource), 1) = "\" Or Mid(strSource, Len(strSource), 1) = "/" Then
                Return strSource.Substring(0, Len(strSource) - 1)
            Else
                Return strSource
            End If
        End Function

        Public Shared Function StripFolderPath(ByVal strOrigPath As String) As String
            If strOrigPath.IndexOf("\") <> -1 Then
                Return Replace(strOrigPath, "0\", "", 1, 1)
            Else
                Return Replace(strOrigPath, "0", "", 1, 1)
            End If
        End Function

        Public Shared Function FormatFolderPath(ByVal strFolderPath As String) As String
            If strFolderPath = "" Then
                Return ""
            Else
                If strFolderPath.EndsWith("/") Then
                    Return strFolderPath
                Else
                    Return strFolderPath & "/"
                End If
            End If
        End Function

#End Region

#Region "Public Methods"

        ''' -----------------------------------------------------------------------------
        ''' <summary>
        ''' Adds a Folder
        ''' </summary>
        ''' <param name="_PortalSettings">Portal Settings for the Portal</param>
        ''' <param name="parentFolder">The Parent Folder Name</param>
        ''' <param name="newFolder">The new Folder Name</param>
        ''' <remarks>
        ''' </remarks>
        ''' <history>
        ''' 	[cnurse]	12/4/2004	Created
        ''' </history>
        ''' -----------------------------------------------------------------------------
        Public Shared Sub AddFolder(ByVal _PortalSettings As PortalSettings, ByVal parentFolder As String, ByVal newFolder As String)
            AddFolder(_PortalSettings, parentFolder, newFolder, FolderController.StorageLocationTypes.InsecureFileSystem)
        End Sub

        ''' -----------------------------------------------------------------------------
        ''' <summary>
        ''' Adds a Folder
        ''' </summary>
        ''' <param name="_PortalSettings">Portal Settings for the Portal</param>
        ''' <param name="parentFolder">The Parent Folder Name</param>
        ''' <param name="newFolder">The new Folder Name</param>
        ''' <param name="StorageLocation">The Storage Location</param>
        ''' <remarks>
        ''' </remarks>
        ''' <history>
        ''' 	[cnurse]	12/4/2004	Created
        ''' </history>
        ''' -----------------------------------------------------------------------------
        Public Shared Sub AddFolder(ByVal _PortalSettings As PortalSettings, ByVal parentFolder As String, ByVal newFolder As String, ByVal StorageLocation As Integer)

            Dim PortalId As Integer
            Dim ParentFolderName As String
            Dim dinfo As New System.IO.DirectoryInfo(parentFolder)
            Dim dinfoNew As System.IO.DirectoryInfo

            If _PortalSettings.ActiveTab.ParentId = _PortalSettings.SuperTabId Then
                PortalId = Null.NullInteger
                ParentFolderName = Common.Globals.HostMapPath
            Else
                PortalId = _PortalSettings.PortalId
                ParentFolderName = _PortalSettings.HomeDirectoryMapPath
            End If

            dinfoNew = New System.IO.DirectoryInfo(parentFolder & newFolder)
            If Not dinfoNew.Exists Then
                dinfoNew = dinfo.CreateSubdirectory(newFolder)
            End If

            Dim FolderPath As String = dinfoNew.FullName.Substring(ParentFolderName.Length).Replace("\", "/")

            'Persist in Database
            AddFolder(PortalId, FolderPath, StorageLocation)

        End Sub

        ''' -----------------------------------------------------------------------------
        ''' <summary>
        ''' Adds a File to a Zip File
        ''' </summary>
        ''' <history>
        ''' 	[cnurse]	12/4/2004	Created
        ''' </history>
        ''' -----------------------------------------------------------------------------
        Public Shared Sub AddToZip(ByRef ZipFile As ZipOutputStream, ByVal filePath As String, ByVal fileName As String, ByVal folder As String)
            Dim crc As Crc32 = New Crc32

            'Open File Stream
            Dim fs As FileStream = File.OpenRead(filePath)

            'Read file into byte array buffer
            Dim buffer As Byte()
            ReDim buffer(Convert.ToInt32(fs.Length) - 1)
            fs.Read(buffer, 0, buffer.Length)

            'Create Zip Entry
            Dim entry As ZipEntry = New ZipEntry(folder & fileName)
            entry.DateTime = DateTime.Now
            entry.Size = fs.Length
            fs.Close()
            crc.Reset()
            crc.Update(buffer)
            entry.Crc = crc.Value

            'Compress file and add to Zip file
            ZipFile.PutNextEntry(entry)
            ZipFile.Write(buffer, 0, buffer.Length)

        End Sub

        ''' -----------------------------------------------------------------------------
        ''' <summary>
        ''' Copies a File
        ''' </summary>
        ''' <param name="strSourceFile">The original File Name</param>
        ''' <param name="strDestFile">The new File Name</param>
        ''' <param name="settings">The Portal Settings for the Portal/Host Account</param>
        ''' <remarks>
        ''' </remarks>
        ''' <history>
        ''' 	[cnurse]	12/2/2004	Created
        ''' </history>
        ''' -----------------------------------------------------------------------------
        Public Shared Function CopyFile(ByVal strSourceFile As String, ByVal strDestFile As String, ByVal settings As PortalSettings) As String
            Return UpdateFile(strSourceFile, strDestFile, GetFolderPortalId(settings), True, False, True)
        End Function

        ''' -----------------------------------------------------------------------------
        ''' <summary>
        ''' This checks to see if the folder is a protected type of folder 
        ''' </summary>
        ''' <param name="folderPath">String</param>
        ''' <returns>Boolean</returns>
        ''' <remarks>
        ''' </remarks>
        ''' <history>
        ''' 	[cpaterra]	4/7/2006	Created
        ''' </history>
        ''' -----------------------------------------------------------------------------
        Public Shared Function DefaultProtectedFolders(ByVal folderPath As String) As Boolean
            If folderPath = "" Or folderPath.ToLower = "skins" Or folderPath.ToLower = "containers" Or folderPath.ToLower.StartsWith("skins/") = True Or folderPath.ToLower.StartsWith("containers/") = True Then
                Return True
            Else
                Return False
            End If
        End Function

        ''' -----------------------------------------------------------------------------
        ''' <summary>
        ''' Trys to delete a file from the file system
        ''' </summary>
        ''' <param name="strFileName">The name of the file</param>
        ''' <history>
        '''     [cnurse]    04/26/2006  Created
        ''' </history>
        ''' -----------------------------------------------------------------------------
        Public Shared Sub DeleteFile(ByVal strFileName As String)
            If File.Exists(strFileName) Then
                File.SetAttributes(strFileName, FileAttributes.Normal)
                File.Delete(strFileName)
            End If
        End Sub

        ''' -----------------------------------------------------------------------------
        ''' <summary>
        ''' Deletes a file
        ''' </summary>
        ''' <param name="strSourceFile">The File to delete</param>
        ''' <param name="settings">The Portal Settings for the Portal/Host Account</param>
        ''' <remarks>
        ''' </remarks>
        ''' <history>
        ''' 	[Jon Henning]	11/1/2004	Created
        '''     [cnurse]        12/6/2004   delete file from db
        ''' </history>
        ''' -----------------------------------------------------------------------------
        Public Shared Function DeleteFile(ByVal strSourceFile As String, ByVal settings As PortalSettings) As String
            Return DeleteFile(strSourceFile, settings, True)
        End Function

        ''' -----------------------------------------------------------------------------
        ''' <summary>
        ''' Deletes a file
        ''' </summary>
        ''' <param name="strSourceFile">The File to delete</param>
        ''' <param name="settings">The Portal Settings for the Portal/Host Account</param>
        ''' <remarks>
        ''' </remarks>
        ''' <history>
        ''' 	[Jon Henning]	11/1/2004	Created
        '''     [cnurse]        12/6/2004   delete file from db
        ''' </history>
        ''' -----------------------------------------------------------------------------
        Public Shared Function DeleteFile(ByVal strSourceFile As String, ByVal settings As PortalSettings, ByVal ClearCache As Boolean) As String

            Dim retValue As String = ""
            Dim folderName As String = GetSubFolderPath(strSourceFile, settings.PortalId)
            Dim fileName As String = GetFileName(strSourceFile)
            Dim PortalId As Integer = GetFolderPortalId(settings)

            If FolderPermissionController.HasFolderPermission(PortalId, folderName, "WRITE") Then
                Try
                    'try and delete the Insecure file
                    DeleteFile(strSourceFile)

                    'try and delete the Secure file
                    DeleteFile(strSourceFile & glbProtectedExtension)

                    'Remove file from DataBase
                    Dim objFileController As New DotNetNuke.Services.FileSystem.FileController
                    Dim objFolders As New FolderController
                    Dim objFolder As FolderInfo = objFolders.GetFolder(PortalId, folderName, False)
                    objFileController.DeleteFile(PortalId, fileName, objFolder.FolderID, ClearCache)

                Catch ex As Exception
                    retValue = ex.Message
                End Try
            Else ' insufficient folder permission in the application
                retValue += "<br>" & String.Format(Localization.GetString("InsufficientFolderPermission"), folderName)
            End If
            Return retValue
        End Function

        ''' -----------------------------------------------------------------------------
        ''' <summary>
        ''' Deletes a folder
        ''' </summary>
        ''' <param name="PortalId">The Id of the Portal</param>
        ''' <param name="folder">The Directory Info object to delete</param>
        ''' <param name="folderName">The Name of the folder relative to the Root of the Portal</param>
        ''' <remarks>
        ''' </remarks>
        ''' <history>
        ''' 	[cnurse]	12/4/2004	Created
        ''' </history>
        ''' -----------------------------------------------------------------------------
        Public Shared Sub DeleteFolder(ByVal PortalId As Integer, ByVal folder As System.IO.DirectoryInfo, ByVal folderName As String)

            'Delete Folder
            folder.Delete(False)

            'Remove Folder from DataBase
            Dim objFolderController As New DotNetNuke.Services.FileSystem.FolderController
            objFolderController.DeleteFolder(PortalId, folderName.Replace("\", "/"))

        End Sub

        ''' -----------------------------------------------------------------------------
        ''' <summary>
        ''' Moved directly from FileManager code, probably should make extension lookup more generic
        ''' </summary>
        ''' <param name="FileLoc">File Location</param>
        ''' <remarks>
        ''' </remarks>
        ''' <history>
        ''' 	[Jon Henning]	11/1/2004	Created
        ''' 	[Jon Henning]	1/4/2005	Fixed extension comparison, added content length header - DNN-386
        ''' </history>
        ''' -----------------------------------------------------------------------------
        Public Shared Sub DownloadFile(ByVal FileLoc As String)
            Dim objFile As New System.IO.FileInfo(FileLoc)
            Dim objResponse As System.Web.HttpResponse = System.Web.HttpContext.Current.Response
            Dim filename As String = objFile.Name
            If HttpContext.Current.Request.UserAgent.IndexOf("; MSIE ") > 0 Then
                filename = HttpUtility.UrlEncode(filename)
            End If
            If objFile.Exists Then
                objResponse.ClearContent()
                objResponse.ClearHeaders()
                objResponse.AppendHeader("content-disposition", "attachment; filename=""" & filename & """")
                objResponse.AppendHeader("Content-Length", objFile.Length.ToString())

                objResponse.ContentType = GetContentType(objFile.Extension.Replace(".", ""))

                WriteFile(objFile.FullName)

                objResponse.Flush()
                objResponse.End()
            End If
        End Sub

        ''' -----------------------------------------------------------------------------
        ''' <summary>
        ''' Streams a file to the output stream if the user has the proper permissions
        ''' </summary>
        ''' <param name="settings">Portal Settings</param>
        ''' <param name="FileId">FileId identifying file in database</param>
        ''' <param name="ClientCache">Cache file in client browser - true/false</param>
        ''' <param name="ForceDownload">Force Download File dialog box - true/false</param>
        ''' <history>
        ''' </history>
        ''' -----------------------------------------------------------------------------
        Public Shared Function DownloadFile(ByVal settings As PortalSettings, ByVal FileId As Integer, ByVal ClientCache As Boolean, ByVal ForceDownload As Boolean) As Boolean

            Return DownloadFile(GetFolderPortalId(settings), FileId, ClientCache, ForceDownload)

        End Function

        ''' -----------------------------------------------------------------------------
        ''' <summary>
        ''' Streams a file to the output stream if the user has the proper permissions
        ''' </summary>
        ''' <param name="PortalId">The Id of the Portal to which the file belongs</param>
        ''' <param name="FileId">FileId identifying file in database</param>
        ''' <param name="ClientCache">Cache file in client browser - true/false</param>
        ''' <param name="ForceDownload">Force Download File dialog box - true/false</param>
        ''' <history>
        ''' </history>
        ''' -----------------------------------------------------------------------------
        Public Shared Function DownloadFile(ByVal PortalId As Integer, ByVal FileId As Integer, ByVal ClientCache As Boolean, ByVal ForceDownload As Boolean) As Boolean
            Dim blnDownload As Boolean = False

            ' get file
            Dim objFiles As New FileController
            Dim objFile As DotNetNuke.Services.FileSystem.FileInfo = objFiles.GetFileById(FileId, PortalId)
            Dim filename As String = objFile.FileName
            If HttpContext.Current.Request.UserAgent.IndexOf("; MSIE ") > 0 Then
                filename = HttpUtility.UrlEncode(filename)
            End If
            If Not objFile Is Nothing Then
                ' check folder view permissions
                If PortalSecurity.IsInRoles(FileSystemUtils.GetRoles(objFile.Folder, PortalId, "READ")) Then
                    ' auto sync
                    Dim blnFileExists As Boolean = True
                    If DotNetNuke.Entities.Host.HostSettings.GetHostSetting("EnableFileAutoSync") <> "N" Then
                        Dim strFile As String = ""
                        Select Case objFile.StorageLocation
                            Case FolderController.StorageLocationTypes.InsecureFileSystem
                                strFile = objFile.PhysicalPath
                            Case FolderController.StorageLocationTypes.SecureFileSystem
                                strFile = objFile.PhysicalPath & glbProtectedExtension
                        End Select
                        If strFile <> "" Then
                            ' synchronize file
                            Dim objFileInfo As System.IO.FileInfo = New System.IO.FileInfo(strFile)
                            If objFileInfo.Exists Then
                                If objFile.Size <> objFileInfo.Length Then
                                    objFile.Size = CType(objFileInfo.Length, Integer)
                                    UpdateFileData(FileId, objFile.FolderId, PortalId, objFile.FileName, objFile.Extension, GetContentType(objFile.Extension), objFileInfo.Length, objFile.Folder)
                                End If
                            Else ' file does not exist
                                RemoveOrphanedFile(objFile, PortalId)
                                blnFileExists = False
                            End If
                        End If
                    End If

                    ' download file
                    If blnFileExists Then
                        ' save script timeout
                        Dim scriptTimeOut As Integer = HttpContext.Current.Server.ScriptTimeout

                        ' temporarily set script timeout to large value ( this value is only applicable when application is not running in Debug mode )
                        HttpContext.Current.Server.ScriptTimeout = Integer.MaxValue

                        Dim objResponse As HttpResponse = HttpContext.Current.Response

                        objResponse.ClearContent()
                        objResponse.ClearHeaders()

                        ' force download dialog
                        If ForceDownload Then
                            objResponse.AppendHeader("content-disposition", "attachment; filename=""" & filename & """")
                        Else
                            'use proper file name when browser forces download because of file type (save as name should match file name)
                            objResponse.AppendHeader("content-disposition", "inline; filename=""" & filename & """")
                        End If
                        objResponse.AppendHeader("Content-Length", objFile.Size.ToString())
                        objResponse.ContentType = GetContentType(objFile.Extension.Replace(".", ""))

                        'Stream the file to the response
                        Dim objStream As IO.Stream = FileSystemUtils.GetFileStream(objFile)
                        Try
                            WriteStream(objResponse, objStream)
                        Catch ex As Exception
                            ' Trap the error, if any.
                            objResponse.Write("Error : " & ex.Message)
                        Finally
                            If IsNothing(objStream) = False Then
                                ' Close the file.
                                objStream.Close()
                            End If
                        End Try

                        objResponse.Flush()
                        objResponse.End()

                        ' reset script timeout
                        HttpContext.Current.Server.ScriptTimeout = scriptTimeOut

                        blnDownload = True
                    End If
                End If
            End If

            Return blnDownload

        End Function

        ''' -----------------------------------------------------------------------------
        ''' <summary>
        ''' gets the content type based on the extension
        ''' </summary>
        ''' <param name="extension">The extension</param>
        ''' <remarks>
        ''' </remarks>
        ''' <history>
        ''' 	[cnurse]	04/26/2006	Created
        ''' </history>
        ''' -----------------------------------------------------------------------------
        Public Shared Function GetContentType(ByVal extension As String) As String

            Dim contentType As String

            Select Case extension.ToLower
                Case "txt" : contentType = "text/plain"
                Case "htm", "html" : contentType = "text/html"
                Case "rtf" : contentType = "text/richtext"
                Case "jpg", "jpeg" : contentType = "image/jpeg"
                Case "gif" : contentType = "image/gif"
                Case "bmp" : contentType = "image/bmp"
                Case "mpg", "mpeg" : contentType = "video/mpeg"
                Case "avi" : contentType = "video/avi"
                Case "pdf" : contentType = "application/pdf"
                Case "doc", "dot" : contentType = "application/msword"
                Case "csv", "xls", "xlt" : contentType = "application/x-msexcel"
                Case Else : contentType = "application/octet-stream"
            End Select

            Return contentType

        End Function

        Public Shared Function GetFileContent(ByVal objFile As DotNetNuke.Services.FileSystem.FileInfo) As Byte()

            Dim objStream As Stream = FileSystemUtils.GetFileStream(objFile)
            Dim objBinaryReader As BinaryReader = New BinaryReader(objStream)
            Dim objContent As Byte() = objBinaryReader.ReadBytes(CType(objStream.Length, Integer))
            objBinaryReader.Close()

            Return objContent

        End Function

        Public Shared Function GetFileStream(ByVal objFile As DotNetNuke.Services.FileSystem.FileInfo) As Stream

            Dim objFileController As New DotNetNuke.Services.FileSystem.FileController
            Dim fileStream As Stream = Nothing

            Select Case objFile.StorageLocation
                Case FolderController.StorageLocationTypes.InsecureFileSystem
                    ' read from file system 
                    fileStream = New FileStream(objFile.PhysicalPath, FileMode.Open, FileAccess.Read, FileShare.Read)
                Case FolderController.StorageLocationTypes.SecureFileSystem
                    ' read from file system 
                    fileStream = New FileStream(objFile.PhysicalPath & glbProtectedExtension, FileMode.Open, FileAccess.Read, FileShare.Read)
                Case FolderController.StorageLocationTypes.DatabaseSecure
                    ' read from database 
                    fileStream = New MemoryStream(objFileController.GetFileContent(objFile.FileId, objFile.PortalId))
            End Select

            Return fileStream

        End Function

        Public Shared Function GetFilesByFolder(ByVal PortalId As Integer, ByVal folderId As Integer) As ArrayList
            Dim objFileController As New FileController
            Return CBO.FillCollection(objFileController.GetFiles(PortalId, folderId), GetType(DotNetNuke.Services.FileSystem.FileInfo))
        End Function

        Public Shared Function GetFolderPortalId(ByVal settings As PortalSettings) As Integer
            Dim FolderPortalId As Integer = Null.NullInteger
            Dim isHost As Boolean = CType(IIf(settings.ActiveTab.ParentId = settings.SuperTabId, True, False), Boolean)
            If Not isHost Then
                FolderPortalId = settings.PortalId
            End If
            Return FolderPortalId
        End Function

        ''' -----------------------------------------------------------------------------
        ''' <summary>
        ''' Gets all the folders for a Portal
        ''' </summary>
        ''' <param name="PortalId">The Id of the Portal</param>
        ''' <remarks>
        ''' </remarks>
        ''' <history>
        ''' 	[cnurse]	04/22/2006	Created
        ''' </history>
        ''' -----------------------------------------------------------------------------
        Public Shared Function GetFolders(ByVal PortalID As Integer) As ArrayList
            Dim objFolderController As New FolderController
            Dim arrFolders As New ArrayList
            For Each folderPair As KeyValuePair(Of String, FolderInfo) In objFolderController.GetFolders(PortalID)
                arrFolders.Add(folderPair.Value)
            Next
            Return arrFolders
        End Function

        Public Shared Function GetFolder(ByVal PortalID As Integer, ByVal FolderPath As String) As FolderInfo
            Dim objFolderController As New FolderController
            Dim objFolder As FolderInfo = objFolderController.GetFolder(PortalID, FolderPath, False)
            If DotNetNuke.Entities.Host.HostSettings.GetHostSetting("EnableFileAutoSync") <> "N" Then
                ' synchronize files in folder
                If Not objFolder Is Nothing Then
                    SynchronizeFolder(objFolder.PortalID, objFolder.PhysicalPath, objFolder.FolderPath, False, True, False)
                End If
            End If
            Return objFolder
        End Function

        ''' -----------------------------------------------------------------------------
        ''' <summary>
        ''' Gets all the subFolders for a Parent
        ''' </summary>
        ''' <param name="PortalId">The Id of the Portal</param>
        ''' <remarks>
        ''' </remarks>
        ''' <history>
        ''' 	[cnurse]	04/22/2006	Created
        ''' </history>
        ''' -----------------------------------------------------------------------------
        Public Shared Function GetFoldersByParentFolder(ByVal PortalId As Integer, ByVal ParentFolder As String) As ArrayList

            Dim folders As ArrayList = GetFolders(PortalId)
            Dim subFolders As New ArrayList
            For Each folder As FolderInfo In folders
                Dim strfolderPath As String = folder.FolderPath
                If folder.FolderPath.IndexOf(ParentFolder) > -1 AndAlso _
                        folder.FolderPath <> Null.NullString AndAlso _
                        folder.FolderPath <> ParentFolder Then
                    If ParentFolder = Null.NullString Then
                        If strfolderPath.IndexOf("/") = strfolderPath.LastIndexOf("/") Then
                            subFolders.Add(folder)
                        End If
                    ElseIf strfolderPath.StartsWith(ParentFolder) Then
                        strfolderPath = strfolderPath.Substring(ParentFolder.Length + 1)
                        If strfolderPath.IndexOf("/") = strfolderPath.LastIndexOf("/") Then
                            subFolders.Add(folder)
                        End If
                    End If
                End If
            Next

            Return subFolders

        End Function

        Public Shared Function GetFoldersByUser(ByVal PortalID As Integer, ByVal IncludeSecure As Boolean, ByVal IncludeDatabase As Boolean, ByVal Permissions As String) As ArrayList
            Dim objFolderController As New FolderController
            Dim arrFolders As New ArrayList

            'Get all the folders for the Portal
            For Each folderPair As KeyValuePair(Of String, FolderInfo) In objFolderController.GetFolders(PortalID)
                Dim canAdd As Boolean = True
                Select Case folderPair.Value.StorageLocation
                    Case FolderController.StorageLocationTypes.DatabaseSecure
                        canAdd = IncludeDatabase
                    Case FolderController.StorageLocationTypes.SecureFileSystem
                        canAdd = IncludeSecure
                End Select

                If canAdd AndAlso PortalID > Null.NullInteger Then
                    'Check Folder Permissions for Portal Folders
                    For Each permissionKey As String In Permissions.Split(","c)
                        canAdd = FolderPermissionController.HasFolderPermission(PortalID, folderPair.Value.FolderPath, permissionKey)
                        If canAdd Then
                            Exit For
                        End If
                    Next
                End If

                If canAdd Then
                    arrFolders.Add(folderPair.Value)
                End If
            Next

            Return arrFolders

        End Function

        ''' -----------------------------------------------------------------------------
        ''' <summary>
        ''' Gets the Roles that have a particualr Permission for a Folder
        ''' </summary>
        ''' <param name="Folder">The Folder</param>
        ''' <param name="PortalId">The Id of the Portal</param>
        ''' <param name="Permission">The Permissions to find</param>
        ''' <remarks>
        ''' </remarks>
        ''' <history>
        ''' </history>
        ''' -----------------------------------------------------------------------------
        Public Shared Function GetRoles(ByVal Folder As String, ByVal PortalId As Integer, ByVal Permission As String) As String

            Dim Roles As New System.Text.StringBuilder
            Dim objFolderPermissionController As New DotNetNuke.Security.Permissions.FolderPermissionController

            Dim objCurrentFolderPermissions As DotNetNuke.Security.Permissions.FolderPermissionCollection
            objCurrentFolderPermissions = objFolderPermissionController.GetFolderPermissionsCollectionByFolderPath(PortalId, Folder)
            Dim objFolderPermission As DotNetNuke.Security.Permissions.FolderPermissionInfo
            For Each objFolderPermission In objCurrentFolderPermissions
                If objFolderPermission.AllowAccess And objFolderPermission.PermissionKey = Permission Then
                    If Null.IsNull(objFolderPermission.UserID) Then
                        Roles.Append(objFolderPermission.RoleName)
                    Else
                        Roles.Append("[" & objFolderPermission.UserID.ToString & "]")
                    End If
                    Roles.Append(";")
                End If
            Next
            Return Roles.ToString
        End Function

        ''' -----------------------------------------------------------------------------
        ''' <summary>
        ''' Moves (Renames) a File
        ''' </summary>
        ''' <param name="strSourceFile">The original File Name</param>
        ''' <param name="strDestFile">The new File Name</param>
        ''' <param name="settings">The Portal Settings for the Portal/Host Account</param>
        ''' <remarks>
        ''' </remarks>
        ''' <history>
        ''' 	[cnurse]	12/2/2004	Created
        ''' </history>
        ''' -----------------------------------------------------------------------------
        Public Shared Function MoveFile(ByVal strSourceFile As String, ByVal strDestFile As String, ByVal settings As PortalSettings) As String
            Return UpdateFile(strSourceFile, strDestFile, GetFolderPortalId(settings), False, False, True)
        End Function

        Public Shared Sub RemoveOrphanedFolders(ByVal PortalId As Integer)
            Dim objFolderController As New DotNetNuke.Services.FileSystem.FolderController
            Dim objFolder As FolderInfo
            Dim arrFolders As ArrayList = GetFolders(PortalId)
            For Each objFolder In arrFolders
                If objFolder.StorageLocation <> FolderController.StorageLocationTypes.DatabaseSecure Then
                    If Directory.Exists(objFolder.PhysicalPath) = False Then
                        RemoveOrphanedFiles(objFolder, PortalId)
                        objFolderController.DeleteFolder(PortalId, objFolder.FolderPath)
                    End If
                End If
            Next
        End Sub

        Public Shared Sub SaveFile(ByVal FullFileName As String, ByVal Buffer As Byte())
            If System.IO.File.Exists(FullFileName) Then
                System.IO.File.SetAttributes(FullFileName, FileAttributes.Normal)
            End If
            Dim fs As New FileStream(FullFileName, FileMode.Create, FileAccess.Write)
            fs.Write(Buffer, 0, Buffer.Length)
            fs.Close()
        End Sub

        ''' -----------------------------------------------------------------------------
        ''' <summary>
        ''' Assigns 1 or more attributes to a file
        ''' </summary>
        ''' <param name="FileLoc">File Location</param>
        ''' <param name="FileAttributesOn">Pass in Attributes you wish to switch on (i.e. FileAttributes.Hidden + FileAttributes.ReadOnly)</param>
        ''' <remarks>
        ''' </remarks>
        ''' <history>
        ''' 	[Jon Henning]	11/1/2004	Created
        ''' </history>
        ''' -----------------------------------------------------------------------------
        Public Shared Sub SetFileAttributes(ByVal FileLoc As String, ByVal FileAttributesOn As Integer)
            System.IO.File.SetAttributes(FileLoc, CType(FileAttributesOn, FileAttributes))
        End Sub

        ''' -----------------------------------------------------------------------------
        ''' <summary>
        ''' Sets a Folders Permissions to the Administrator Role
        ''' </summary>
        ''' <param name="PortalId">The Id of the Portal</param>
        ''' <param name="FolderId">The Id of the Folder</param>
        ''' <param name="AdministratorRoleId">The Id of the Administrator Role</param>
        ''' <param name="relativePath">The folder's Relative Path</param>
        ''' <remarks>
        ''' </remarks>
        ''' <history>
        ''' 	[cnurse]	12/4/2004	Created
        ''' </history>
        ''' -----------------------------------------------------------------------------
        Public Shared Sub SetFolderPermissions(ByVal PortalId As Integer, ByVal FolderId As Integer, ByVal AdministratorRoleId As Integer, ByVal relativePath As String)

            'Set Permissions
            Dim objPermissionController As New PermissionController
            Dim Permissions As ArrayList = objPermissionController.GetPermissionsByFolder(PortalId, "")
            For Each objPermssionsInfo As PermissionInfo In Permissions
                SetFolderPermission(PortalId, FolderId, objPermssionsInfo.PermissionID, AdministratorRoleId, relativePath)
            Next

        End Sub

        ''' -----------------------------------------------------------------------------
        ''' <summary>
        ''' Sets a Folders Permissions the same as the Folders parent folder
        ''' </summary>
        ''' <param name="PortalId">The Id of the Portal</param>
        ''' <param name="FolderId">The Id of the Folder</param>
        ''' <param name="relativePath">The folder's Relative Path</param>
        ''' <remarks>
        ''' </remarks>
        ''' <history>
        ''' 	[cnurse]	08/01/2006	Created
        ''' </history>
        ''' -----------------------------------------------------------------------------
        Public Shared Sub SetFolderPermissions(ByVal PortalId As Integer, ByVal FolderId As Integer, ByVal relativePath As String)
            If relativePath <> "" Then
                Dim parentFolderPath As String = relativePath.Substring(0, relativePath.Substring(0, relativePath.Length - 1).LastIndexOf("/") + 1)

                'Get Parents permissions
                Dim objFolderPermissionController As New FolderPermissionController
                Dim objFolderPermissions As FolderPermissionCollection
                objFolderPermissions = objFolderPermissionController.GetFolderPermissionsCollectionByFolderPath(PortalId, parentFolderPath)

                'Iterate parent permissions to see if permisison has already been added
                For Each objPermission As FolderPermissionInfo In objFolderPermissions
                    SetFolderPermission(PortalId, FolderId, objPermission.PermissionID, objPermission.RoleID, objPermission.UserID, relativePath)
                Next
            End If
        End Sub

        Public Shared Sub SetFolderPermission(ByVal PortalId As Integer, ByVal FolderId As Integer, ByVal PermissionId As Integer, ByVal RoleId As Integer, ByVal relativePath As String)
            SetFolderPermission(PortalId, FolderId, PermissionId, RoleId, Null.NullInteger, relativePath)
        End Sub

        ''' -----------------------------------------------------------------------------
        ''' <summary>
        ''' Sets a Folder Permission
        ''' </summary>
        ''' <param name="PortalId">The Id of the Portal</param>
        ''' <param name="FolderId">The Id of the Folder</param>
        ''' <param name="PermissionId">The Id of the Permission</param>
        ''' <param name="RoleId">The Id of the Role</param>
        ''' <param name="relativePath">The folder's Relative Path</param>
        ''' <remarks>
        ''' </remarks>
        ''' <history>
        ''' 	[cnurse]	01/12/2005	Created
        ''' </history>
        ''' -----------------------------------------------------------------------------
        Public Shared Sub SetFolderPermission(ByVal PortalId As Integer, ByVal FolderId As Integer, ByVal PermissionId As Integer, ByVal RoleId As Integer, ByVal UserId As Integer, ByVal relativePath As String)

            Dim objFolderPermissionController As New FolderPermissionController
            Dim objCurrentFolderPermissions As FolderPermissionCollection
            Dim objFolderPermissionInfo As New FolderPermissionInfo
            objCurrentFolderPermissions = objFolderPermissionController.GetFolderPermissionsCollectionByFolderPath(PortalId, relativePath)

            'Iterate current permissions to see if permisison has already been added
            For Each objFolderPermissionInfo In objCurrentFolderPermissions
                If objFolderPermissionInfo.FolderID = FolderId And _
                    objFolderPermissionInfo.PermissionID = PermissionId And _
                    objFolderPermissionInfo.RoleID = RoleId And _
                    objFolderPermissionInfo.UserID = UserId And _
                    objFolderPermissionInfo.AllowAccess = True Then

                    Exit Sub
                End If
            Next

            'Permission not found so Add
            objFolderPermissionInfo = CType(CBO.InitializeObject(objFolderPermissionInfo, GetType(FolderPermissionInfo)), FolderPermissionInfo)
            objFolderPermissionInfo.FolderID = FolderId
            objFolderPermissionInfo.PermissionID = PermissionId
            objFolderPermissionInfo.RoleID = RoleId
            objFolderPermissionInfo.UserID = UserId
            objFolderPermissionInfo.AllowAccess = True
            objFolderPermissionController.AddFolderPermission(objFolderPermissionInfo)
        End Sub

        Public Shared Sub Synchronize(ByVal PortalId As Integer, ByVal AdministratorRoleId As Integer, ByVal HomeDirectory As String)
            Dim PhysicalRoot As String = HomeDirectory
            Dim VirtualRoot As String = ""

            'Call Synchronize Folder that recursively parses the subfolders
            SynchronizeFolder(PortalId, PhysicalRoot, VirtualRoot, True)

            'Invalidate Cache
            DataCache.ClearFolderCache(PortalId)
        End Sub

        Public Shared Sub SynchronizeFolder(ByVal PortalId As Integer, ByVal physicalPath As String, ByVal relativePath As String, ByVal isRecursive As Boolean)
            SynchronizeFolder(PortalId, physicalPath, relativePath, isRecursive, True, True)
        End Sub

        Public Shared Sub SynchronizeFolder(ByVal PortalId As Integer, ByVal physicalPath As String, ByVal relativePath As String, ByVal isRecursive As Boolean, ByVal syncFiles As Boolean, ByVal forceFolderSync As Boolean)
            Dim objFolderController As New FolderController()
            Dim FolderId As Integer
            Dim isInSync As Boolean = True

            ' synchronize folder collection
            If forceFolderSync = True And relativePath = "" Then
                RemoveOrphanedFolders(PortalId)
            End If

            'Attempt to get the folder
            Dim folder As FolderInfo = objFolderController.GetFolder(PortalId, relativePath, False)

            Dim dirInfo As New DirectoryInfo(physicalPath)
            If dirInfo.Exists Then
                ' check to see if the folder exists in the db
                If folder Is Nothing Then
                    'Add Folder to database
                    FolderId = AddFolder(PortalId, relativePath, FolderController.StorageLocationTypes.InsecureFileSystem)
                    folder = objFolderController.GetFolder(PortalId, relativePath, True)
                    isInSync = False
                Else
                    'Check whether existing folder is in sync by comparing LastWriteTime of the physical folder with the LastUpdated value in the database
                    '*NOTE: dirInfo.LastWriteTime is updated when files are added to or deleted from a directory - but NOT when existing files are overwritten ( this is a known Windows Operating System issue )
                    isInSync = (dirInfo.LastWriteTime.ToString("yyyyMMddhhmmss") = folder.LastUpdated.ToString("yyyyMMddhhmmss"))
                End If

                If Not folder Is Nothing Then
                    If syncFiles = True And (isInSync = False Or forceFolderSync = True) Then
                        'Get Physical Files in this Folder and sync them
                        Dim strFiles As String() = Directory.GetFiles(physicalPath)
                        For Each strFileName As String In strFiles
                            'Add the File if it doesn't exist, Update it if the file size has changed
                            AddFile(strFileName, PortalId, False, folder)
                        Next

                        'Removed orphaned files
                        RemoveOrphanedFiles(folder, PortalId)

                        'Update the folder with the LastWriteTime of the directory
                        folder.LastUpdated = dirInfo.LastWriteTime
                        objFolderController.UpdateFolder(folder)
                    End If

                    'Get Physical Sub Folders (and synchronize recursively)
                    If isRecursive Then
                        Dim strFolders As String() = Directory.GetDirectories(physicalPath)
                        For Each strFolder As String In strFolders
                            Dim dir As New DirectoryInfo(strFolder)
                            Dim relPath As String = Null.NullString
                            If relativePath = "" Then
                                relPath = dir.Name & "/"
                            Else
                                relPath = relativePath
                                If Not relativePath.EndsWith("/") Then
                                    relPath = relPath & "/"
                                End If
                                relPath = relPath & dir.Name & "/"
                            End If
                            SynchronizeFolder(PortalId, strFolder, relPath, True, syncFiles, forceFolderSync)
                        Next
                    End If
                End If
            Else ' physical folder does not exist on file system
                If Not folder Is Nothing Then
                    ' folder exists in DB
                    If folder.StorageLocation <> FolderController.StorageLocationTypes.DatabaseSecure Then
                        ' remove files and folder from DB
                        RemoveOrphanedFiles(folder, PortalId)
                        objFolderController.DeleteFolder(PortalId, relativePath.Replace("\", "/"))
                    End If
                End If
            End If

        End Sub

        ''' -----------------------------------------------------------------------------
        ''' <summary>
        ''' Unzips a File
        ''' </summary>
        ''' <param name="fileName">The zip File Name</param>
        ''' <param name="DestFolder">The folder where the file is extracted to</param>
        ''' <param name="settings">The Portal Settings for the Portal/Host Account</param>
        ''' <remarks>
        ''' </remarks>
        ''' <history>
        ''' </history>
        ''' -----------------------------------------------------------------------------
        Public Shared Function UnzipFile(ByVal fileName As String, ByVal DestFolder As String, ByVal settings As PortalSettings) As String

            Dim FolderPortalId As Integer = GetFolderPortalId(settings)
            Dim isHost As Boolean = CType(IIf(settings.ActiveTab.ParentId = settings.SuperTabId, True, False), Boolean)

            Dim objPortalController As New PortalController
            Dim objFolderController As New DotNetNuke.Services.FileSystem.FolderController
            Dim objFileController As New DotNetNuke.Services.FileSystem.FileController
            Dim sourceFolderName As String = GetSubFolderPath(fileName, settings.PortalId)
            Dim sourceFileName As String = GetFileName(fileName)
            Dim folder As FolderInfo = objFolderController.GetFolder(FolderPortalId, sourceFolderName, False)
            Dim file As DotNetNuke.Services.FileSystem.FileInfo = objFileController.GetFile(sourceFileName, FolderPortalId, folder.FolderID)
            Dim storageLocation As Integer = folder.StorageLocation
            Dim objZipInputStream As ZipInputStream
            Dim objZipEntry As ZipEntry
            Dim strMessage As String = ""
            Dim strFileName As String = ""
            Dim strExtension As String

            'Get the source Content from wherever it is

            'Create a Zip Input Stream
            Try
                objZipInputStream = New ZipInputStream(GetFileStream(file))
            Catch ex As Exception
                Return ex.Message
            End Try

            Dim sortedFolders As New ArrayList

            objZipEntry = objZipInputStream.GetNextEntry

            'iterate folders
            While Not objZipEntry Is Nothing
                If objZipEntry.IsDirectory Then
                    Try
                        sortedFolders.Add(objZipEntry.Name.ToString)
                    Catch ex As Exception
                        objZipInputStream.Close()
                        Return ex.Message
                    End Try
                End If
                objZipEntry = objZipInputStream.GetNextEntry
            End While

            sortedFolders.Sort()

            For Each s As String In sortedFolders
                Try
                    AddFolder(settings, DestFolder, s.ToString, storageLocation)
                Catch ex As Exception
                    Return ex.Message
                End Try
            Next

            'Recreate the Zip Input Stream and parse it for the files
            objZipInputStream = New ZipInputStream(GetFileStream(file))
            objZipEntry = objZipInputStream.GetNextEntry
            While Not objZipEntry Is Nothing
                If Not objZipEntry.IsDirectory Then
                    If objPortalController.HasSpaceAvailable(FolderPortalId, objZipEntry.Size) Then
                        strFileName = Path.GetFileName(objZipEntry.Name)
                        If strFileName <> "" Then
                            strExtension = Path.GetExtension(strFileName).Replace(".", "")
                            If InStr(1, "," & settings.HostSettings("FileExtensions").ToString.ToLower, "," & strExtension.ToLower) <> 0 Or isHost Then
                                Try
                                    Dim folderPath As String = System.IO.Path.GetDirectoryName(DestFolder & Replace(objZipEntry.Name, "/", "\"))
                                    Dim Dinfo As New DirectoryInfo(folderPath)
                                    If Not Dinfo.Exists Then
                                        AddFolder(settings, DestFolder, objZipEntry.Name.Substring(0, Replace(objZipEntry.Name, "/", "\").LastIndexOf("\")))
                                    End If

                                    Dim zipEntryFileName As String = DestFolder & Replace(objZipEntry.Name, "/", "\")
                                    strMessage += AddFile(FolderPortalId, objZipInputStream, zipEntryFileName, "", objZipEntry.Size, GetSubFolderPath(zipEntryFileName, settings.PortalId), False, False)
                                Catch ex As Exception
                                    If Not objZipInputStream Is Nothing Then
                                        objZipInputStream.Close()
                                    End If
                                    Return ex.Message
                                End Try
                            Else
                                ' restricted file type
                                strMessage += "<br>" & String.Format(Localization.GetString("RestrictedFileType"), strFileName, Replace(settings.HostSettings("FileExtensions").ToString, ",", ", *."))
                            End If
                        End If
                    Else    ' file too large
                        strMessage += "<br>" & String.Format(Localization.GetString("DiskSpaceExceeded"), strFileName)
                    End If

                End If

                objZipEntry = objZipInputStream.GetNextEntry
            End While

            objZipInputStream.Close()

            Return strMessage

        End Function

        Public Shared Sub UnzipResources(ByVal zipStream As ZipInputStream, ByVal destPath As String)
            Dim objZipEntry As ZipEntry
            Dim LocalFileName, RelativeDir, FileNamePath As String

            objZipEntry = zipStream.GetNextEntry
            While Not objZipEntry Is Nothing
                ' This gets the Zipped FileName (including the path)
                LocalFileName = objZipEntry.Name

                ' This creates the necessary directories if they don't 
                ' already exist.
                RelativeDir = Path.GetDirectoryName(objZipEntry.Name)
                If (RelativeDir <> String.Empty) AndAlso (Not Directory.Exists(Path.Combine(destPath, RelativeDir))) Then
                    Directory.CreateDirectory(Path.Combine(destPath, RelativeDir))
                End If

                ' This block creates the file using buffered reads from the zipfile
                If (Not objZipEntry.IsDirectory) AndAlso (LocalFileName <> "") Then
                    FileNamePath = Path.Combine(destPath, LocalFileName).Replace("/", "\")

                    Try
                        ' delete the file if it already exists
                        If File.Exists(FileNamePath) Then
                            File.SetAttributes(FileNamePath, FileAttributes.Normal)
                            File.Delete(FileNamePath)
                        End If

                        ' create the file
                        Dim objFileStream As FileStream = File.Create(FileNamePath)

                        Dim intSize As Integer = 2048
                        Dim arrData(2048) As Byte

                        intSize = zipStream.Read(arrData, 0, arrData.Length)
                        While intSize > 0
                            objFileStream.Write(arrData, 0, intSize)
                            intSize = zipStream.Read(arrData, 0, arrData.Length)
                        End While

                        objFileStream.Close()
                    Catch
                        ' an error occurred saving a file in the resource file
                    End Try

                End If

                objZipEntry = zipStream.GetNextEntry
            End While
            If Not zipStream Is Nothing Then
                zipStream.Close()
            End If

        End Sub

        ''' <summary>
        ''' UploadFile pocesses a single file 
        ''' </summary>
        ''' <param name="RootPath">The folder wherr the file will be put</param>
        ''' <param name="objHtmlInputFile">The file to upload</param>
        ''' <param name="Unzip"></param> 
        Public Shared Function UploadFile(ByVal RootPath As String, ByVal objHtmlInputFile As HttpPostedFile, Optional ByVal Unzip As Boolean = False) As String
            Return UploadFile(RootPath, objHtmlInputFile, Null.NullString, Unzip)
        End Function

        ''' -----------------------------------------------------------------------------
        ''' <summary>
        ''' UploadFile pocesses a single file 
        ''' </summary>
        ''' <param name="RootPath">The folder wherr the file will be put</param>
        ''' <param name="objHtmlInputFile">The file to upload</param>
        ''' <param name="NewFileName"></param>
        ''' <param name="Unzip"></param> 
        ''' <remarks>
        ''' </remarks>
        ''' <history>
        '''     [cnurse]        16/9/2004   Updated for localization, Help and 508
        '''     [Philip Beadle] 10/06/2004  Moved to Globals from WebUpload.ascx.vb so can be accessed by URLControl.ascx
        '''     [cnurse]        04/26/2006  Updated for Secure Storage
        '''     [sleupold]      08/14/2007  Added NewFileName
        ''' </history>
        ''' -----------------------------------------------------------------------------
        Public Shared Function UploadFile(ByVal RootPath As String, ByVal objHtmlInputFile As HttpPostedFile, ByVal NewFileName As String, Optional ByVal Unzip As Boolean = False) As String

            ' Obtain PortalSettings from Current Context
            Dim settings As PortalSettings = PortalController.GetCurrentPortalSettings
            Dim PortalId As Integer = GetFolderPortalId(settings)
            Dim isHost As Boolean = CType(IIf(settings.ActiveTab.ParentId = settings.SuperTabId, True, False), Boolean)

            Dim objPortalController As New PortalController
            Dim strMessage As String = ""
            Dim strFileName As String = objHtmlInputFile.FileName
            If NewFileName <> Null.NullString Then strFileName = NewFileName
            strFileName = RootPath & Path.GetFileName(strFileName)
            Dim strExtension As String = Replace(Path.GetExtension(strFileName), ".", "")
            Dim strFolderpath As String = GetSubFolderPath(strFileName, PortalId)

            If FolderPermissionController.HasFolderPermission(PortalId, strFolderpath, "WRITE") Then
                If objPortalController.HasSpaceAvailable(PortalId, objHtmlInputFile.ContentLength) Then
                    If InStr(1, "," & settings.HostSettings("FileExtensions").ToString.ToUpper, "," & strExtension.ToUpper) <> 0 Or isHost Then
                        'Save Uploaded file to server
                        Try
                            strMessage += AddFile(PortalId, objHtmlInputFile.InputStream, strFileName, objHtmlInputFile.ContentType, objHtmlInputFile.ContentLength, strFolderpath, True, True)

                            'Optionally Unzip File?
                            If Path.GetExtension(strFileName).ToLower = ".zip" And Unzip = True Then
                                strMessage += UnzipFile(strFileName, RootPath, settings)
                            End If
                        Catch Exc As Exception
                            ' save error - can happen if the security settings are incorrect on the disk
                            strMessage += "<br>" & String.Format(Localization.GetString("SaveFileError"), strFileName)
                        End Try
                    Else
                        ' restricted file type
                        strMessage += "<br>" & String.Format(Localization.GetString("RestrictedFileType"), strFileName, Replace(settings.HostSettings("FileExtensions").ToString, ",", ", *."))
                    End If
                Else    ' file too large
                    strMessage += "<br>" & String.Format(Localization.GetString("DiskSpaceExceeded"), strFileName)
                End If
            Else ' insufficient folder permission in the application
                strMessage += "<br>" & String.Format(Localization.GetString("InsufficientFolderPermission"), strFolderpath)
            End If


            Return strMessage
        End Function

        ''' -----------------------------------------------------------------------------
        ''' <summary>
        ''' Writes file to response stream.  Workaround offered by MS for large files
        ''' http://support.microsoft.com/default.aspx?scid=kb;EN-US;812406
        ''' </summary>
        ''' <param name="strFileName">FileName</param>
        ''' <remarks>
        ''' </remarks>
        ''' <history>
        ''' 	[Jon Henning]	1/4/2005	Created
        ''' </history>
        ''' -----------------------------------------------------------------------------
        Public Shared Sub WriteFile(ByVal strFileName As String)
            Dim objResponse As System.Web.HttpResponse = System.Web.HttpContext.Current.Response
            Dim objStream As System.IO.Stream = Nothing

            Try
                ' Open the file.
                objStream = New System.IO.FileStream(strFileName, System.IO.FileMode.Open, IO.FileAccess.Read, IO.FileShare.Read)

                WriteStream(objResponse, objStream)
            Catch ex As Exception
                ' Trap the error, if any.
                objResponse.Write("Error : " & ex.Message)
            Finally
                If IsNothing(objStream) = False Then
                    ' Close the file.
                    objStream.Close()
                End If
            End Try
        End Sub

        Public Shared Function DeleteFiles(ByVal arrPaths As Array) As String
            Dim strPath As String
            Dim strExceptions As String = ""

            For Each strPath In arrPaths
                ' remove comment
                If strPath.IndexOf("'") <> -1 Then
                    strPath = strPath.Substring(0, strPath.IndexOf("'"))
                End If

                If strPath.Trim <> "" Then
                    strPath = Common.Globals.ApplicationMapPath & "\" & strPath
                    If strPath.EndsWith("\") Then
                        ' folder
                        If Directory.Exists(strPath) Then
                            Try ' delete the folder
                                DotNetNuke.Common.DeleteFolderRecursive(strPath)
                            Catch ex As Exception
                                strExceptions += "Error: " & ex.Message & vbCrLf
                            End Try
                        End If
                    Else
                        ' file
                        If File.Exists(strPath) Then
                            Try ' delete the file
                                File.SetAttributes(strPath, FileAttributes.Normal)
                                File.Delete(strPath)
                            Catch ex As Exception
                                strExceptions += "Error: " & ex.Message & vbCrLf
                            End Try
                        End If
                    End If
                End If
            Next

            Return strExceptions
        End Function

#End Region

#Region "Obsoleted Methods, retained for Binary Compatability"

        <Obsolete("This function has been replaced by GetFileContent(FileInfo)")> _
        Public Shared Function GetFileContent(ByVal objFile As DotNetNuke.Services.FileSystem.FileInfo, ByVal PortalId As Integer, ByVal HomeDirectory As String) As Byte()
            Return GetFileContent(objFile)
        End Function

        <Obsolete("This function has been replaced by GetFilesByFolder(PortalId, FolderId)")> _
        Public Shared Function GetFilesByFolder(ByVal PortalId As Integer, ByVal folderPath As String) As ArrayList
            Dim objFolders As New FolderController
            Dim objFolder As FolderInfo = objFolders.GetFolder(PortalId, folderPath, False)
            If objFolder Is Nothing Then
                Return Nothing
            End If
            Return GetFilesByFolder(PortalId, objFolder.FolderID)
        End Function

        <Obsolete("This function has been replaced by GetFileStream(FileInfo)")> _
        Public Shared Function GetFileStream(ByVal objFile As DotNetNuke.Services.FileSystem.FileInfo, ByVal PortalId As Integer, ByVal HomeDirectory As String) As Stream
            Return GetFileStream(objFile)
        End Function

        <Obsolete("This function has been replaced by GetFoldersByUser(ByVal PortalID As Integer, ByVal IncludeSecure As Boolean, ByVal IncludeDatabase As Boolean, ByVal Permissions As String)")> _
        Public Shared Function GetFoldersByUser(ByVal PortalID As Integer, ByVal IncludeSecure As Boolean, ByVal IncludeDatabase As Boolean, ByVal AllowAccess As Boolean, ByVal Permissions As String) As ArrayList
            Return GetFoldersByUser(PortalID, IncludeSecure, IncludeDatabase, Permissions)
        End Function

        <Obsolete("This function has been replaced by SynchronizeFolder(Integer, Integer, String, String, Boolean)")> _
        Public Shared Sub SynchronizeFolder(ByVal PortalId As Integer, ByVal AdministratorRoleId As Integer, ByVal HomeDirectory As String, ByVal physicalPath As String, ByVal relativePath As String, ByVal isRecursive As Boolean)
            SynchronizeFolder(PortalId, physicalPath, relativePath, isRecursive)
        End Sub

#End Region

    End Class

End Namespace
