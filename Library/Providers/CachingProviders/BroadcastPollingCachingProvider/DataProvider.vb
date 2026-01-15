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
Imports System.Data
Imports System.Web.Caching
Imports System.Reflection
Imports DotNetNuke.Services.Log.EventLog
Imports DotNetNuke.Services.Cache

Namespace DotNetNuke.Services.Cache.BroadcastPollingCachingProvider.Data

    Public MustInherit Class DataProvider

#Region "Shared/Static Methods"
        ' provider constants - eliminates need for Reflection later
        Private Const ProviderType As String = "data"    ' maps to <sectionGroup> in web.config
        Private Const ProviderNamespace As String = "DotNetNuke.Services.Cache.BroadcastPollingCachingProvider.Data"    ' project namespace
        Private Const ProviderAssemblyName As String = ""    ' project assemblyname

        ' singleton reference to the instantiated object 
        Private Shared objProvider As DataProvider = Nothing

        ' constructor
        Shared Sub New()
            CreateProvider()
        End Sub

        ' dynamically create provider
        Private Shared Sub CreateProvider()
            objProvider = CType(Framework.Reflection.CreateObject(ProviderType, ProviderNamespace, ProviderAssemblyName), DataProvider)
        End Sub

        ' return the provider
        Public Shared Shadows Function Instance() As DataProvider
            Return objProvider
        End Function

#End Region

#Region "Abstract Methods"

        Public MustOverride Function GetCachedObject(ByVal Key As String) As IDataReader
        Public MustOverride Sub AddCachedObject(ByVal Key As String, ByVal Value As String, ByVal ServerName As String)
        Public MustOverride Sub DeleteCachedObject(ByVal Key As String)

        Public MustOverride Function AddBroadcast(ByVal BroadcastType As String, ByVal BroadcastMessage As String, ByVal ServerName As String) As Integer
        Public MustOverride Function GetBroadcasts(ByVal ServerName As String) As IDataReader

#End Region

    End Class

End Namespace
