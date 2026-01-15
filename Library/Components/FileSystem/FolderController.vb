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

#Region "Imports Statements"

Imports System
Imports System.Collections.Generic
Imports System.Configuration
Imports System.Data
Imports System.Globalization
Imports System.Web

#End Region

Namespace DotNetNuke.Services.FileSystem

    ''' -----------------------------------------------------------------------------
    ''' Project	 : DotNetNuke
    ''' Class	 : FolderController
    ''' 
    ''' -----------------------------------------------------------------------------
    ''' <summary>
    ''' Business Class that provides access to the Database for the functions within the calling classes
    ''' Instantiates the instance of the DataProvider and returns the object, if any
    ''' </summary>
    ''' <remarks>
    ''' </remarks>
    ''' -----------------------------------------------------------------------------
    Public Class FolderController

#Region "Enumerators"

        Enum StorageLocationTypes
            InsecureFileSystem = 0
            SecureFileSystem = 1
            DatabaseSecure = 2
        End Enum

#End Region

#Region "Private methods"

        Private Function FillFolderInfo(ByVal dr As IDataReader) As FolderInfo
            Return FillFolderInfo(dr, True)
        End Function

        Private Function FillFolderInfo(ByVal dr As IDataReader, ByVal CheckForOpenDataReader As Boolean) As FolderInfo
            Dim folder As FolderInfo = Nothing

            ' read datareader
            Dim canContinue As Boolean = True
            If CheckForOpenDataReader Then
                canContinue = False
                If dr.Read Then
                    canContinue = True
                End If
            End If
            If canContinue Then
                folder = New FolderInfo
                folder.FolderID = Convert.ToInt32(Null.SetNull(dr("FolderID"), folder.FolderID))
                folder.PortalID = Convert.ToInt32(Null.SetNull(dr("PortalID"), folder.PortalID))
                folder.FolderPath = Convert.ToString(Null.SetNull(dr("FolderPath"), folder.FolderPath))
                folder.IsCached = Convert.ToBoolean(Null.SetNull(dr("IsCached"), folder.IsCached))
                folder.IsProtected = Convert.ToBoolean(Null.SetNull(dr("IsProtected"), folder.IsProtected))
                folder.StorageLocation = Convert.ToInt32(Null.SetNull(dr("StorageLocation"), folder.StorageLocation))
                folder.LastUpdated = Convert.ToDateTime(Null.SetNull(dr("LastUpdated"), folder.LastUpdated))
            End If

            Return folder
        End Function

        Private Function FillFolderInfoDictionary(ByVal dr As IDataReader) As Dictionary(Of String, FolderInfo)
            Dim dic As New Dictionary(Of String, FolderInfo)
            Try
                Dim obj As FolderInfo
                While dr.Read
                    ' fill business object
                    obj = FillFolderInfo(dr, False)
                    ' add to dictionary
                    dic(obj.FolderPath) = obj
                End While
            Catch exc As Exception
                LogException(exc)
            Finally
                ' close datareader
                If Not dr Is Nothing Then
                    dr.Close()
                End If
            End Try

            Return dic
        End Function

        Private Sub UpdateParentFolder(ByVal PortalID As Integer, ByVal FolderPath As String)

            If FolderPath.Length > 0 Then
                Dim parentFolderPath As String = FolderPath.Substring(0, FolderPath.Substring(0, FolderPath.Length - 1).LastIndexOf("/") + 1)
                Dim objFolder As FolderInfo = GetFolder(PortalID, parentFolderPath, False)
                If Not objFolder Is Nothing Then
                    UpdateFolder(objFolder)
                End If
            End If

        End Sub

#End Region

#Region "Public Methods"

        Public Function AddFolder(ByVal PortalID As Integer, ByVal FolderPath As String) As Integer
            Return AddFolder(PortalID, FolderPath, StorageLocationTypes.InsecureFileSystem, False, False)
        End Function

        Public Function AddFolder(ByVal PortalID As Integer, ByVal FolderPath As String, ByVal StorageLocation As Integer, ByVal IsProtected As Boolean, ByVal IsCached As Boolean) As Integer
            Return AddFolder(PortalID, FolderPath, StorageLocation, IsProtected, IsCached, Null.NullDate)
        End Function

        Public Function AddFolder(ByVal PortalID As Integer, ByVal FolderPath As String, ByVal StorageLocation As Integer, ByVal IsProtected As Boolean, ByVal IsCached As Boolean, ByVal LastUpdated As Date) As Integer

            FolderPath = FileSystemUtils.FormatFolderPath(FolderPath)

            Dim FolderId As Integer

            Dim folder As FolderInfo = GetFolder(PortalID, FolderPath, True)
            If folder Is Nothing Then
                FolderId = DataProvider.Instance().AddFolder(PortalID, FolderPath, StorageLocation, IsProtected, IsCached, LastUpdated)
                UpdateParentFolder(PortalID, FolderPath)
            Else
                FolderId = folder.FolderID
                DataProvider.Instance().UpdateFolder(PortalID, FolderId, FolderPath, StorageLocation, IsProtected, IsCached, LastUpdated)
            End If

            'Invalidate Cache
            DataCache.ClearFolderCache(PortalID)

            Return FolderId

        End Function

        Public Sub DeleteFolder(ByVal PortalID As Integer, ByVal FolderPath As String)
            DataProvider.Instance().DeleteFolder(PortalID, FileSystemUtils.FormatFolderPath(FolderPath))
            UpdateParentFolder(PortalID, FolderPath)

            'Invalidate Cache
            DataCache.ClearFolderCache(PortalID)
        End Sub

        Public Function GetFolder(ByVal PortalID As Integer, ByVal FolderPath As String, ByVal ignoreCache As Boolean) As FolderInfo
            FolderPath = FileSystemUtils.FormatFolderPath(FolderPath)

            Dim folder As FolderInfo = Nothing
            Dim bFound As Boolean = False
            If Not ignoreCache Then
                Dim dicFolders As Dictionary(Of String, FolderInfo)
                'First try the cache
                dicFolders = GetFolders(PortalID)
                bFound = dicFolders.TryGetValue(FolderPath, folder)
            End If

            If ignoreCache Or Not bFound Then
                Dim dr As IDataReader = DataProvider.Instance().GetFolder(PortalID, FolderPath)
                Try
                    folder = FillFolderInfo(dr)
                Finally
                    If Not dr Is Nothing Then
                        dr.Close()
                    End If
                End Try
            End If
            Return folder
        End Function

        Public Function GetFolders(ByVal PortalID As Integer) As Dictionary(Of String, FolderInfo)
            Dim key As String = String.Format(DataCache.FolderCacheKey, PortalID)

            'First Check the Folder Cache
            Dim folders As Dictionary(Of String, FolderInfo) = TryCast(DataCache.GetCache(key), Dictionary(Of String, FolderInfo))

            If folders Is Nothing Then
                'folder caching settings
                Dim timeOut As Int32 = DataCache.FolderCacheTimeOut * Convert.ToInt32(Common.Globals.PerformanceSetting)

                'Get folders form Database
                folders = FillFolderInfoDictionary(DataProvider.Instance().GetFoldersByPortal(PortalID))

                'Cache tabs
                If timeOut > 0 Then
                    DataCache.SetCache(key, folders, TimeSpan.FromMinutes(timeOut), False)
                End If
            End If

            Return folders
        End Function

        Public Function GetFolderInfo(ByVal PortalID As Integer, ByVal FolderID As Integer) As FolderInfo
            Dim folder As FolderInfo = Nothing
            Dim dr As IDataReader = DataProvider.Instance().GetFolder(PortalID, FolderID)
            Try
                folder = FillFolderInfo(dr)
            Finally
                If Not dr Is Nothing Then
                    dr.Close()
                End If
            End Try
            Return folder
        End Function

        Public Function GetMappedDirectory(ByVal VirtualDirectory As String) As String
            Dim MappedDir As String = Convert.ToString(DataCache.GetCache("DirMap:" + VirtualDirectory))
            Try
                If MappedDir = "" Then
                    MappedDir = FileSystemUtils.AddTrailingSlash(HttpContext.Current.Server.MapPath(VirtualDirectory))
                    DataCache.SetCache("DirMap:" + VirtualDirectory, MappedDir)
                End If
            Catch exc As Exception
                LogException(exc)
            End Try
            Return MappedDir
        End Function

        Public Sub SetMappedDirectory(ByVal VirtualDirectory As String)
            Try
                Dim MappedDir As String = FileSystemUtils.AddTrailingSlash(HttpContext.Current.Server.MapPath(VirtualDirectory))
                DataCache.SetCache("DirMap:" + VirtualDirectory, MappedDir)
            Catch exc As Exception
                LogException(exc)
            End Try
        End Sub

        Public Sub SetMappedDirectory(ByVal VirtualDirectory As String, ByVal context As HttpContext)
            Try
                Dim MappedDir As String = FileSystemUtils.AddTrailingSlash(context.Server.MapPath(VirtualDirectory))
                DataCache.SetCache("DirMap:" + VirtualDirectory, MappedDir)
            Catch exc As Exception
                LogException(exc)
            End Try
        End Sub

        Public Sub SetMappedDirectory(ByVal portalInfo As PortalInfo, ByVal context As HttpContext)
            Try
                Dim VirtualDirectory As String = Common.Globals.ApplicationPath + "/" + portalInfo.HomeDirectory + "/"
                SetMappedDirectory(VirtualDirectory, context)

            Catch exc As Exception
                LogException(exc)
            End Try
        End Sub

        Public Sub UpdateFolder(ByVal objFolderInfo As FolderInfo)
            DataProvider.Instance().UpdateFolder(objFolderInfo.PortalID, objFolderInfo.FolderID, FileSystemUtils.FormatFolderPath(objFolderInfo.FolderPath), objFolderInfo.StorageLocation, objFolderInfo.IsProtected, objFolderInfo.IsCached, objFolderInfo.LastUpdated)

            'Invalidate Cache
            DataCache.ClearFolderCache(objFolderInfo.PortalID)
        End Sub

#End Region

        <Obsolete("This method is obsolete.  It has been replaced by GetFolderInfo(ByVal PortalID As Integer, ByVal FolderID As Integer) As FolderInfo ")> _
        Public Function GetFolder(ByVal PortalID As Integer, ByVal FolderID As Integer) As ArrayList
            Dim arrFolders As New ArrayList
            Dim folder As FolderInfo = GetFolderInfo(PortalID, FolderID)
            If Not folder Is Nothing Then
                arrFolders.Add(folder)
            End If
            Return arrFolders
        End Function

        <Obsolete("This method is obsolete.  It has been replaced by GetFolderInfo(ByVal PortalID As Integer, ByVal FolderID As Integer, ByVal ignoreCache As Boolean) ")> _
        Public Function GetFolder(ByVal PortalID As Integer, ByVal FolderPath As String) As FolderInfo
            Return GetFolder(PortalID, FolderPath, True)
        End Function

        <Obsolete("This method is obsolete.  It has been replaced by GetFolders(ByVal PortalID As Integer) ")> _
        Public Function GetFoldersByPortal(ByVal PortalID As Integer) As ArrayList
            Dim arrFolders As New ArrayList
            For Each folderPair As KeyValuePair(Of String, FolderInfo) In GetFolders(PortalID)
                arrFolders.Add(folderPair.Value)
            Next
            Return arrFolders
        End Function

    End Class

End Namespace
