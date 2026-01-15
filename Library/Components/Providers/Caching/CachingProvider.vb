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
Imports System.Web.Caching


Namespace DotNetNuke.Services.Cache

    Public MustInherit Class CachingProvider


#Region "Shared/Static Methods"

        ' singleton reference to the instantiated object 
        Private Shared objProvider As CachingProvider = Nothing

        ' constructor
        Shared Sub New()
            CreateProvider()
        End Sub

        ' dynamically create provider
        Private Shared Sub CreateProvider()
            objProvider = CType(Framework.Reflection.CreateObject("caching", False), CachingProvider)
        End Sub

        ' return the provider
        Public Shared Shadows Function Instance() As CachingProvider
            Return objProvider
        End Function

#End Region

#Region "Abstract Methods"

        ' methods to return functionality support indicators
        Public MustOverride Function Add(ByVal Key As String, ByVal Value As Object, ByVal Dependencies As CacheDependency, ByVal AbsoluteExpiration As DateTime, ByVal SlidingExpiration As TimeSpan, ByVal Priority As CacheItemPriority, ByVal OnRemoveCallback As CacheItemRemovedCallback) As Object
        Public MustOverride Function GetEnumerator() As IDictionaryEnumerator
        Public MustOverride Function GetItem(ByVal CacheKey As String) As Object
        Public MustOverride Function GetPersistentCacheItem(ByVal CacheKey As String, ByVal objType As Type) As Object
        Public MustOverride Sub Insert(ByVal CacheKey As String, ByVal objObject As Object, ByVal PersistAppRestart As Boolean)
        Public MustOverride Sub Insert(ByVal CacheKey As String, ByVal objObject As Object, ByVal objDependency As CacheDependency, ByVal PersistAppRestart As Boolean)
        Public MustOverride Sub Insert(ByVal CacheKey As String, ByVal objObject As Object, ByVal objDependency As CacheDependency, ByVal AbsoluteExpiration As Date, ByVal SlidingExpiration As System.TimeSpan, ByVal PersistAppRestart As Boolean)
        Public MustOverride Sub Insert(ByVal CacheKey As String, ByVal objObject As Object, ByVal objDependency As CacheDependency, ByVal AbsoluteExpiration As Date, ByVal SlidingExpiration As System.TimeSpan, ByVal Priority As CacheItemPriority, ByVal OnRemoveCallback As CacheItemRemovedCallback, ByVal PersistAppRestart As Boolean)
        Public MustOverride Sub Remove(ByVal CacheKey As String)
        Public MustOverride Sub RemovePersistentCacheItem(ByVal CacheKey As String)
        Public MustOverride Function PurgeCache() As String

#End Region

    End Class

End Namespace
