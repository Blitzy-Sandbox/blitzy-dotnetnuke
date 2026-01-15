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
Imports System.Collections.Generic
Imports System.Data
Imports System.IO
Imports System.Web.Caching

Imports DotNetNuke.Entities.Tabs
Imports DotNetNuke.Services.Cache

Namespace DotNetNuke.Common.Utilities

    Public Enum CoreCacheType
        Host = 1
        Portal = 2
        Tab = 3
    End Enum

    Public Class DataCache

        Private Shared strCachePersistenceEnabled As String = ""

#Region "Public Constants"

        Public Const PortalDictionaryCacheKey As String = "PortalDictionary"
        Public Const PortalDictionaryTimeOut As Integer = 20

        Public Const PortalCacheKey As String = "Portal{0}"
        Public Const PortalCacheTimeOut As Integer = 20

        Public Const TabCacheKey As String = "Tabs{0}"
        Public Const TabCacheTimeOut As Integer = 20
        Public Const TabPathCacheKey As String = "TabPathDictionary"

        Public Const TabPermissionCacheKey As String = "TabPermissions{0}"
        Public Const TabPermissionCacheTimeOut As Integer = 20

        Public Const TabModuleCacheKey As String = "TabModules{0}"
        Public Const TabModuleCacheTimeOut As Integer = 20

        Public Const ModulePermissionCacheKey As String = "ModulePermissions{0}"
        Public Const ModulePermissionCacheTimeOut As Integer = 20

        Public Const ModuleCacheKey As String = "Modules{0}"
        Public Const ModuleCacheTimeOut As Integer = 20

        Public Const FolderCacheKey As String = "Folders{0}"
        Public Const FolderCacheTimeOut As Integer = 20

        Public Const FolderPermissionCacheKey As String = "FolderPermissions{0}"
        Public Const FolderPermissionCacheTimeOut As Integer = 20

        Public Const ListsCacheKey As String = "Lists{0}"
        Public Const ListsCacheTimeOut As Integer = 20

        Public Const ProfileDefinitionsCacheKey As String = "ProfileDefinitions{0}"
        Public Const ProfileDefinitionsCacheTimeOut As Integer = 20

        Public Const UserCacheKey As String = "UserInfo|{0}|{1}"
        Public Const UserCacheTimeOut As Integer = 1

#End Region

#Region "Private Methods"

        Private Shared Sub ClearTabCache(ByVal TabId As Integer, ByVal PortalId As Integer)
            ClearModuleCache(TabId)
            ClearTabPermissionsCache(PortalId)
        End Sub

#End Region

#Region "Public Shared Properties"

        Public Shared ReadOnly Property CachePersistenceEnabled() As Boolean
            Get
                If String.IsNullOrEmpty(strCachePersistenceEnabled) Then
                    If Config.GetSetting("EnableCachePersistence") Is Nothing Then
                        strCachePersistenceEnabled = "false"
                    Else
                        strCachePersistenceEnabled = Config.GetSetting("EnableCachePersistence")
                    End If
                End If
                Return Boolean.Parse(strCachePersistenceEnabled)
            End Get
        End Property

#End Region

#Region "Public Shared Methods"

        Public Shared Sub ClearHostCache(ByVal Cascade As Boolean)
            RemoveCache("GetHostSettings")
            RemoveCache("GetPortalByAlias")
            RemoveCache("CSS")
            ClearFolderCache(-1)
            ClearDefinitionsCache(-1)
            ClearListsCache(-1)
            RemoveCache("CompressionConfig")
            RemoveCache("GetSkins-1")
            If Cascade Then
                Dim objPortals As New PortalController
                Dim objPortal As PortalInfo
                Dim arrPortals As ArrayList = objPortals.GetPortals

                Dim intIndex As Integer
                For intIndex = 0 To arrPortals.Count - 1
                    objPortal = CType(arrPortals(intIndex), PortalInfo)
                    ClearPortalCache(objPortal.PortalID, Cascade)
                Next
            End If
        End Sub

        Public Shared Sub ClearPortalCache(ByVal PortalId As Integer, ByVal Cascade As Boolean)
            RemovePersistentCacheItem(String.Format(PortalCacheKey, PortalId))
            ClearDefinitionsCache(PortalId)
            ClearFolderCache(PortalId)
            RemoveCache("GetSkins" & PortalId.ToString)
            ClearListsCache(PortalId)
            If Cascade Then
                Dim objTabs As New TabController
                For Each tabPair As KeyValuePair(Of Integer, TabInfo) In objTabs.GetTabsByPortal(PortalId)
                    Dim objTab As TabInfo = tabPair.Value
                    ClearModuleCache(objTab.TabID)
                Next
                ClearTabPermissionsCache(PortalId)

                Dim moduleController As New DotNetNuke.Entities.Modules.ModuleController()
                For Each moduleInfo As DotNetNuke.Entities.Modules.ModuleInfo In moduleController.GetModules(PortalId)
                    RemoveCache("GetModuleSettings" & moduleInfo.ModuleID.ToString)
                Next
            End If
            ClearTabsCache(PortalId)
        End Sub

        Public Shared Sub ClearDefinitionsCache(ByVal PortalId As Integer)
            RemoveCache(String.Format(ProfileDefinitionsCacheKey, PortalId))
        End Sub

        Public Shared Sub ClearFolderCache(ByVal PortalId As Integer)
            RemoveCache(String.Format(FolderCacheKey, PortalId))
            ClearFolderPermissionsCache(PortalId)
        End Sub

        Public Shared Sub ClearFolderPermissionsCache(ByVal PortalId As Integer)
            RemoveCache(String.Format(FolderPermissionCacheKey, PortalId))
        End Sub

        Public Shared Sub ClearListsCache(ByVal PortalId As Integer)
            RemoveCache(String.Format(ListsCacheKey, PortalId))
        End Sub

		Public Shared Sub ClearModuleCache()
			Dim objPortals As New PortalController()
			Dim objTabs As New TabController()
			Dim objModules As New DotNetNuke.Entities.Modules.ModuleController()
			Dim objPortal As PortalInfo
			Dim objTab As TabInfo
			Dim objModule As DotNetNuke.Entities.Modules.ModuleInfo
			Dim intIndex As Integer

			Dim arrPortals As ArrayList = objPortals.GetPortals
			For intIndex = 0 To arrPortals.Count - 1
				objPortal = CType(arrPortals(intIndex), PortalInfo)

				For Each tabPair As KeyValuePair(Of Integer, TabInfo) In objTabs.GetTabsByPortal(objPortal.PortalID)
					objTab = tabPair.Value
					ClearModuleCache(objTab.TabID)
					ClearModulePermissionsCache(objTab.TabID)
				Next

				For Each objModule In objModules.GetModules(objPortal.PortalID)
					RemoveCache("GetModuleSettings" & objModule.ModuleID.ToString)
				Next
			Next

		End Sub

        Public Shared Sub ClearModuleCache(ByVal TabId As Integer)
            RemoveCache(String.Format(TabModuleCacheKey, TabId))
            ClearModulePermissionsCache(TabId)
        End Sub

        Public Shared Sub ClearModulePermissionsCache(ByVal TabId As Integer)
            RemovePersistentCacheItem(String.Format(ModulePermissionCacheKey, TabId))
        End Sub

        Public Shared Sub ClearModulePermissionsCachesByPortal(ByVal PortalId As Integer)
            Dim objTabs As New TabController
            For Each tabPair As KeyValuePair(Of Integer, TabInfo) In objTabs.GetTabsByPortal(PortalId)
                Dim objTab As TabInfo = tabPair.Value
                ClearModulePermissionsCache(objTab.TabID)
            Next
        End Sub

        Public Shared Sub ClearTabsCache(ByVal PortalId As Integer)
            RemovePersistentCacheItem(String.Format(TabCacheKey, PortalId))
            RemoveCache(TabPathCacheKey)
            ClearTabPermissionsCache(PortalId)
        End Sub

        Public Shared Sub ClearTabPermissionsCache(ByVal PortalId As Integer)
            RemovePersistentCacheItem(String.Format(TabPermissionCacheKey, PortalId))
        End Sub

        Public Shared Sub ClearUserCache(ByVal PortalId As Integer, ByVal username As String)
            RemoveCache(String.Format(UserCacheKey, PortalId, username))
        End Sub

        Public Shared Function GetCache(ByVal CacheKey As String) As Object
            Return CachingProvider.Instance.GetItem(CacheKey)
        End Function

        Public Shared Function GetPersistentCacheItem(ByVal CacheKey As String, ByVal objType As Type) As Object
            Return CachingProvider.Instance.GetPersistentCacheItem(CacheKey, objType)
        End Function

        Public Shared Sub RemoveCache(ByVal CacheKey As String)
            CachingProvider.Instance.Remove(CacheKey)
        End Sub

        Public Shared Sub RemovePersistentCacheItem(ByVal CacheKey As String)
            CachingProvider.Instance.RemovePersistentCacheItem(CacheKey)
        End Sub

        Public Shared Sub SetCache(ByVal CacheKey As String, ByVal objObject As Object)
            SetCache(CacheKey, objObject, False)
        End Sub

        Public Shared Sub SetCache(ByVal CacheKey As String, ByVal objObject As Object, ByVal PersistAppRestart As Boolean)
            CachingProvider.Instance.Insert(CacheKey, objObject, PersistAppRestart)
        End Sub

        Public Shared Sub SetCache(ByVal CacheKey As String, ByVal objObject As Object, ByVal objDependency As CacheDependency)
            SetCache(CacheKey, objObject, objDependency, False)
        End Sub

        Public Shared Sub SetCache(ByVal CacheKey As String, ByVal objObject As Object, ByVal objDependency As CacheDependency, ByVal PersistAppRestart As Boolean)
            CachingProvider.Instance.Insert(CacheKey, objObject, objDependency, PersistAppRestart)
        End Sub

        Public Shared Sub SetCache(ByVal CacheKey As String, ByVal objObject As Object, ByVal objDependency As CacheDependency, ByVal AbsoluteExpiration As Date, ByVal SlidingExpiration As System.TimeSpan)
            SetCache(CacheKey, objObject, objDependency, AbsoluteExpiration, SlidingExpiration, False)
        End Sub

        Public Shared Sub SetCache(ByVal CacheKey As String, ByVal objObject As Object, ByVal objDependency As CacheDependency, ByVal AbsoluteExpiration As Date, ByVal SlidingExpiration As System.TimeSpan, ByVal PersistAppRestart As Boolean)
            CachingProvider.Instance.Insert(CacheKey, objObject, objDependency, AbsoluteExpiration, SlidingExpiration, PersistAppRestart)
        End Sub

        Public Shared Sub SetCache(ByVal CacheKey As String, ByVal objObject As Object, ByVal SlidingExpiration As TimeSpan)
            SetCache(CacheKey, objObject, SlidingExpiration, False)
        End Sub

        Public Shared Sub SetCache(ByVal CacheKey As String, ByVal objObject As Object, ByVal SlidingExpiration As TimeSpan, ByVal PersistAppRestart As Boolean)
            CachingProvider.Instance.Insert(CacheKey, objObject, Nothing, Cache.NoAbsoluteExpiration, SlidingExpiration, PersistAppRestart)
        End Sub

        Public Shared Sub SetCache(ByVal CacheKey As String, ByVal objObject As Object, ByVal objDependency As CacheDependency, ByVal AbsoluteExpiration As Date, ByVal SlidingExpiration As System.TimeSpan, ByVal Priority As CacheItemPriority, ByVal OnRemoveCallback As CacheItemRemovedCallback)
            SetCache(CacheKey, objObject, objDependency, AbsoluteExpiration, SlidingExpiration, Priority, OnRemoveCallback, False)
        End Sub

        Public Shared Sub SetCache(ByVal CacheKey As String, ByVal objObject As Object, ByVal objDependency As CacheDependency, ByVal AbsoluteExpiration As Date, ByVal SlidingExpiration As System.TimeSpan, ByVal Priority As CacheItemPriority, ByVal OnRemoveCallback As CacheItemRemovedCallback, ByVal PersistAppRestart As Boolean)
            CachingProvider.Instance.Insert(CacheKey, objObject, Nothing, Cache.NoAbsoluteExpiration, SlidingExpiration, PersistAppRestart)
        End Sub

        Public Shared Sub SetCache(ByVal CacheKey As String, ByVal objObject As Object, ByVal AbsoluteExpiration As Date)
            SetCache(CacheKey, objObject, AbsoluteExpiration, False)
        End Sub

        Public Shared Sub SetCache(ByVal CacheKey As String, ByVal objObject As Object, ByVal AbsoluteExpiration As Date, ByVal PersistAppRestart As Boolean)
            CachingProvider.Instance.Insert(CacheKey, objObject, Nothing, AbsoluteExpiration, Cache.NoSlidingExpiration, PersistAppRestart)
        End Sub

#End Region

#Region "Obsolete"

        <Obsolete("This method is obsolete. Use the new specific methods: ClearHostCache, ClearPortalCache, ClearTabCache.")> _
        Public Shared Sub ClearCoreCache(ByVal Type As CoreCacheType, Optional ByVal ID As Integer = -1, Optional ByVal Cascade As Boolean = False)
            Select Case Type
                Case CoreCacheType.Host
                    ClearHostCache(Cascade)
                Case CoreCacheType.Portal
                    ClearPortalCache(ID, Cascade)
                Case CoreCacheType.Tab
                    Dim objTab As TabInfo
                    Dim objTabs As New TabController
                    objTab = objTabs.GetTab(ID, -1, True)
                    If Not objTab Is Nothing Then
                        ClearTabCache(ID, objTab.PortalID)
                    End If
            End Select
        End Sub

#End Region

    End Class

End Namespace