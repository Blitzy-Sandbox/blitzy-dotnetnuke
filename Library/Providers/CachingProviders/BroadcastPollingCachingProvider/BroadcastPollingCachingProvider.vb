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
Imports System.Collections
Imports DotNetNuke.Common.Utilities
Imports DotNetNuke.Framework.Providers
Imports DotNetNuke.Common
Imports DotNetNuke.Services.Exceptions
Imports System.Web.Caching
Imports System.Xml.Serialization

Namespace DotNetNuke.Services.Cache.BroadcastPollingCachingProvider

    Public Class BPCachingProvider
        Inherits CachingProvider

        Private Const ProviderType As String = "caching"
        Private _providerConfiguration As ProviderConfiguration = ProviderConfiguration.GetProviderConfiguration(ProviderType)

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

            Dim obj As Object = objCache(CacheKey)
            If Not obj Is Nothing Then
                Return objCache(CacheKey)
            End If
            Return obj

        End Function
        Public Overrides Function GetPersistentCacheItem(ByVal CacheKey As String, ByVal objType As Type) As Object

            Dim obj As Object = objCache(CacheKey)
            If Not obj Is Nothing Then
                Return obj
            ElseIf DataCache.CachePersistenceEnabled Then
                Dim c As New Controller

                obj = c.GetCachedObject(CacheKey, objType)
                If Not obj Is Nothing Then
                    Insert(CacheKey, obj, True)
                End If

            End If
            Return obj

        End Function

        Public Overloads Overrides Sub Insert(ByVal CacheKey As String, ByVal objObject As Object, ByVal PersistAppRestart As Boolean)

            If PersistAppRestart Then
                'remove the cache key which
                'will remove the serialized
                'file before creating a new one
                Remove(CacheKey)
            End If
            Dim c As New Controller
            If PersistAppRestart And DataCache.CachePersistenceEnabled Then
                c.AddCachedObject(CacheKey, objObject, Common.Globals.ServerName)
                c.AddBroadcast("RemoveCachedItem", CacheKey, Common.Globals.ServerName)
            ElseIf WebFarmEnabled() Then
                c.AddBroadcast("RemoveCachedItem", CacheKey, Common.Globals.ServerName)
            End If

            objCache.Insert(CacheKey, objObject)

        End Sub

        Public Overloads Overrides Sub Insert(ByVal CacheKey As String, ByVal objObject As Object, ByVal objDependency As CacheDependency, ByVal PersistAppRestart As Boolean)

            If PersistAppRestart Then
                'remove the cache key which
                'will remove the serialized
                'file before creating a new one
                Remove(CacheKey)
            End If

            Dim c As New Controller
            If PersistAppRestart And DataCache.CachePersistenceEnabled Then
                c.AddCachedObject(CacheKey, objObject, Common.Globals.ServerName)
                c.AddBroadcast("RemoveCachedItem", CacheKey, Common.Globals.ServerName)
            ElseIf WebFarmEnabled() Then
                c.AddBroadcast("RemoveCachedItem", CacheKey, Common.Globals.ServerName)
            End If

            objCache.Insert(CacheKey, objObject, objDependency)


        End Sub

        Public Overloads Overrides Sub Insert(ByVal CacheKey As String, ByVal objObject As Object, ByVal objDependency As CacheDependency, ByVal AbsoluteExpiration As Date, ByVal SlidingExpiration As System.TimeSpan, ByVal PersistAppRestart As Boolean)

            If PersistAppRestart Then
                'remove the cache key which
                'will remove the serialized
                'file before creating a new one
                Remove(CacheKey)
            End If

            Dim c As New Controller
            If PersistAppRestart And DataCache.CachePersistenceEnabled Then
                c.AddCachedObject(CacheKey, objObject, Common.Globals.ServerName)
                c.AddBroadcast("RemoveCachedItem", CacheKey, Common.Globals.ServerName)
            ElseIf WebFarmEnabled() Then
                c.AddBroadcast("RemoveCachedItem", CacheKey, Common.Globals.ServerName)
            End If

            objCache.Insert(CacheKey, objObject, objDependency, AbsoluteExpiration, SlidingExpiration)

        End Sub

        Public Overloads Overrides Sub Insert(ByVal CacheKey As String, ByVal objObject As Object, ByVal objDependency As CacheDependency, ByVal AbsoluteExpiration As Date, ByVal SlidingExpiration As System.TimeSpan, ByVal Priority As CacheItemPriority, ByVal OnRemoveCallback As CacheItemRemovedCallback, ByVal PersistAppRestart As Boolean)

            If PersistAppRestart Then
                'remove the cache key which
                'will remove the serialized
                'file before creating a new one
                Remove(CacheKey)
            End If

            Dim c As New Controller
            If PersistAppRestart And DataCache.CachePersistenceEnabled Then
                c.AddCachedObject(CacheKey, objObject, Common.Globals.ServerName)
                c.AddBroadcast("RemoveCachedItem", CacheKey, Common.Globals.ServerName)
            ElseIf WebFarmEnabled() Then
                c.AddBroadcast("RemoveCachedItem", CacheKey, Common.Globals.ServerName)
            End If

            objCache.Insert(CacheKey, objObject, objDependency, AbsoluteExpiration, SlidingExpiration, Priority, OnRemoveCallback)

        End Sub
        Public Overrides Sub Remove(ByVal CacheKey As String)

            If Not objCache(CacheKey) Is Nothing Then
                Dim c As New Controller
                objCache.Remove(CacheKey)
                If WebFarmEnabled() Then c.AddBroadcast("RemoveCachedItem", CacheKey, Common.Globals.ServerName)
            End If

        End Sub

        Public Overrides Sub RemovePersistentCacheItem(ByVal CacheKey As String)

            If Not objCache(CacheKey) Is Nothing Then
                objCache.Remove(CacheKey)
                If DataCache.CachePersistenceEnabled = True Then
                    Dim c As New Controller
                    c.DeleteCachedObject(CacheKey)
                    c.AddBroadcast("RemoveCachedItem", CacheKey, Common.Globals.ServerName)
                End If
            End If

        End Sub

        Public Overrides Function PurgeCache() As String
            Return "The Broadcast/Polling-Based Caching Provider does not require the PurgeCache feature."
        End Function


#End Region

#Region "Private Methods"



#End Region


    End Class

End Namespace
