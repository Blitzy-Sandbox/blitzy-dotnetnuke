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
Imports System.IO
Imports System.Web
Imports System.Reflection
Imports System.Threading
Imports DotNetNuke.Common.Utilities
Imports DotNetNuke.Framework.Providers
Imports DotNetNuke.Common
Imports DotNetNuke.Services.Exceptions
Imports System.Web.Caching
Imports System.Xml.Serialization
Imports System.Text

Namespace DotNetNuke.Services.Cache.FileBasedCachingProvider

    Public Class FBCachingProvider
        Inherits CachingProvider

        Private Const ProviderType As String = "caching"
        Private _providerConfiguration As ProviderConfiguration = ProviderConfiguration.GetProviderConfiguration(ProviderType)

        Friend Shared CachingDirectory As String = "Cache\"
        Friend Const CacheFileExtension As String = ".resources"
        Private Shared _objCache As System.Web.Caching.Cache

        Private Shared ReadOnly Property objCache() As System.Web.Caching.Cache
            Get
                'create singleton of the cache object
                If _objCache Is Nothing Then
                    _objCache = HttpRuntime.Cache
                End If
                Return _objCache
            End Get
        End Property

#Region "Abstract Method Implementation"

        Public Overrides Function Add(ByVal Key As String, ByVal Value As Object, ByVal Dependencies As CacheDependency, ByVal AbsoluteExpiration As DateTime, ByVal SlidingExpiration As TimeSpan, ByVal Priority As CacheItemPriority, ByVal OnRemoveCallback As CacheItemRemovedCallback) As Object

            Return objCache.Add(Key, Value, Dependencies, AbsoluteExpiration, SlidingExpiration, Priority, OnRemoveCallback)

        End Function

        Public Overrides Function GetEnumerator() As IDictionaryEnumerator

            Return objCache.GetEnumerator

        End Function

        Public Overrides Function GetItem(ByVal CacheKey As String) As Object

            Return objCache(CacheKey)

        End Function

        Public Overrides Function GetPersistentCacheItem(ByVal CacheKey As String, ByVal objType As Type) As Object

            Return GetItem(CacheKey)

        End Function

        Public Overloads Overrides Sub Insert(ByVal CacheKey As String, ByVal objObject As Object, ByVal PersistAppRestart As Boolean)

            Insert(CacheKey, objObject, Nothing, PersistAppRestart)

        End Sub

        Public Overloads Overrides Sub Insert(ByVal CacheKey As String, ByVal objObject As Object, ByVal objDependency As CacheDependency, ByVal PersistAppRestart As Boolean)

            Insert(CacheKey, objObject, objDependency, Date.MinValue, New TimeSpan(0), PersistAppRestart)

        End Sub

        Public Overloads Overrides Sub Insert(ByVal CacheKey As String, ByVal objObject As Object, ByVal objDependency As CacheDependency, ByVal AbsoluteExpiration As Date, ByVal SlidingExpiration As System.TimeSpan, ByVal PersistAppRestart As Boolean)

            Insert(CacheKey, objObject, objDependency, AbsoluteExpiration, SlidingExpiration, CacheItemPriority.Default, Nothing, PersistAppRestart)

        End Sub

        Public Overloads Overrides Sub Insert(ByVal CacheKey As String, ByVal objObject As Object, ByVal objDependency As CacheDependency, ByVal AbsoluteExpiration As Date, ByVal SlidingExpiration As System.TimeSpan, ByVal Priority As CacheItemPriority, ByVal OnRemoveCallback As CacheItemRemovedCallback, ByVal PersistAppRestart As Boolean)

            ' initialize cache dependency
            Dim d As Caching.CacheDependency = objDependency

            ' if web farm is enabled in config file
            If WebFarmEnabled Then
                ' get hashed file name
                Dim f(0) As String
                f(0) = GetFileName(CacheKey)
                ' create a cache file for item
                CreateCacheFile(f(0), CacheKey)
                ' create a cache dependency on the cache file
                d = New Caching.CacheDependency(f, Nothing, objDependency)
            End If

            ' insert the item into memory using the appropriate overload
            If d Is Nothing Then
                objCache.Insert(CacheKey, objObject)
            ElseIf AbsoluteExpiration = DateTime.MinValue Then
                objCache.Insert(CacheKey, objObject, d)
            ElseIf Priority = CacheItemPriority.Default Then
                objCache.Insert(CacheKey, objObject, d, AbsoluteExpiration, SlidingExpiration)
            Else
                objCache.Insert(CacheKey, objObject, d, AbsoluteExpiration, SlidingExpiration, Priority, OnRemoveCallback)
            End If

        End Sub

        Public Overrides Sub RemovePersistentCacheItem(ByVal CacheKey As String)

            Remove(CacheKey)

        End Sub

        Public Overrides Sub Remove(ByVal CacheKey As String)

            ' remove item from memory
            If Not objCache(CacheKey) Is Nothing Then
                objCache.Remove(CacheKey)
            End If

            ' if web farm is enabled in config file
            If WebFarmEnabled Then
                ' get hashed filename
                Dim f As String = GetFileName(CacheKey)
                ' delete cache file - this synchronizes the cache across servers in the farm
                DeleteCacheFile(f)
            End If

        End Sub

        Public Overrides Function PurgeCache() As String

            ' called by scheduled job to remove cache files which are no longer active
            Return PurgeCacheFiles(HostMapPath + CachingDirectory)

        End Function

#End Region

#Region "Private Methods"

        Private Shared Function GetFileName(ByVal CacheKey As String) As String
            ' cache key may contain characters invalid for a filename - this method creates a valid filename
            Dim FileNameBytes As Byte() = Text.ASCIIEncoding.ASCII.GetBytes(CacheKey)
            Dim md5 As System.Security.Cryptography.MD5CryptoServiceProvider = New System.Security.Cryptography.MD5CryptoServiceProvider()
            FileNameBytes = md5.ComputeHash(FileNameBytes)
            Dim FinalFileName As String = ByteArrayToString(FileNameBytes)
            Return Path.GetFullPath(HostMapPath + CachingDirectory + FinalFileName + CacheFileExtension)
        End Function

        Private Shared Function ByteArrayToString(ByVal arrInput() As Byte) As String
            Dim i As Integer
            Dim sOutput As New StringBuilder(arrInput.Length)
            For i = 0 To arrInput.Length - 1
                sOutput.Append(arrInput(i).ToString("X2"))
            Next
            Return sOutput.ToString()
        End Function

        Private Shared Sub CreateCacheFile(ByVal FileName As String, ByVal CacheKey As String)
            Try
                ' if the cache file does not already exist
                If Not File.Exists(FileName) Then
                    ' declare stream
                    Dim s As StreamWriter
                    ' create the cache file
                    s = File.CreateText(FileName)
                    ' write the CacheKey to the file to provide a documented link between cache item and cache file
                    s.Write(CacheKey)
                    ' close the stream
                    If Not s Is Nothing Then
                        s.Close()
                    End If
                End If
            Catch ex As Exception
                ' permissions issue creating cache file or more than one thread may have been trying to write the cache file simultaneously
                LogException(ex)
            End Try
        End Sub

        Private Shared Sub DeleteCacheFile(ByVal FileName As String)
            Try
                If File.Exists(FileName) Then
                    File.Delete(FileName)
                End If
            Catch ex As Exception
                ' an error occurred when trying to delete the cache file - this is serious as it means that the cache will not be synchronized
                LogException(ex)
            End Try
        End Sub

        Private Function PurgeCacheFiles(ByVal Folder As String) As String
            ' declare counters
            Dim PurgedFiles As Integer = 0
            Dim PurgeErrors As Integer = 0
            Dim i As Integer

            ' get list of cache files
            Dim f() As String
            f = Directory.GetFiles(Folder)

            ' loop through cache files
            For i = 0 To f.Length - 1
                ' get last write time for file
                Dim dtLastWrite As Date
                dtLastWrite = File.GetLastWriteTime(f(i))
                ' if the cache file is more than 2 hours old ( no point in checking most recent cache files )
                If dtLastWrite < Now.Subtract(New TimeSpan(2, 0, 0)) Then
                    ' get cachekey
                    Dim strCacheKey As String = Path.GetFileNameWithoutExtension(f(i))
                    ' if the cache key does not exist in memory
                    If DataCache.GetCache(strCacheKey) Is Nothing Then
                        Try
                            ' delete the file
                            File.Delete(f(i))
                            PurgedFiles += 1
                        Catch ex As Exception
                            ' an error occurred
                            PurgeErrors += 1
                        End Try
                    End If
                End If
            Next

            ' return a summary message for the job
            Return String.Format("Cache Synchronization Files Processed: " + f.Length.ToString + ", Purged: " + PurgedFiles.ToString + ", Errors: " + PurgeErrors.ToString)
        End Function

#End Region


    End Class

End Namespace
