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
Imports System.Configuration
Imports System.Data
Imports System.Globalization

Imports DotNetNuke.Entities.Modules
Imports DotNetNuke.Entities.Portals
Imports System.Xml.Serialization
Imports System.IO
Imports System.Xml
Imports DotNetNuke.Security.Roles
Imports DotNetNuke.Entities.Modules.Definitions
Imports DotNetNuke.Services.EventQueue

Namespace DotNetNuke.Entities.Tabs

    Public Class TabController

#Region "Helper structure for sorting tabs"

        Private Structure TabOrderHelper
            Public TabOrder As Integer
            Public Level As Integer
            Public ParentId As Integer

            Public Sub New(ByVal inttaborder As Integer, ByVal intlevel As Integer, ByVal intparentid As Integer)
                TabOrder = inttaborder
                Level = intlevel
                ParentId = intparentid
            End Sub

        End Structure

#End Region

#Region "Private Methods"

        Private Sub ClearCache(ByVal portalId As Integer)
            DataCache.ClearTabsCache(portalId)

            'Clear the Portal cache so the Pages count is correct
            DataCache.ClearPortalCache(portalId, False)
        End Sub

        Private Function FillTabInfo(ByVal dr As IDataReader) As TabInfo
            Return FillTabInfo(dr, True, False)
        End Function

        Private Function FillTabInfo(ByVal dr As IDataReader, ByVal CheckForOpenDataReader As Boolean, ByVal CheckForLegacyFields As Boolean) As TabInfo
            Dim objTabInfo As New TabInfo
            Dim objTabPermissionController As New Security.Permissions.TabPermissionController

            ' read datareader
            Dim canContinue As Boolean = True
            If CheckForOpenDataReader Then
                canContinue = False
                If dr.Read Then
                    canContinue = True
                End If
            End If
            If canContinue Then
                objTabInfo.TabID = Convert.ToInt32(Null.SetNull(dr("TabID"), objTabInfo.TabID))
                objTabInfo.TabOrder = Convert.ToInt32(Null.SetNull(dr("TabOrder"), objTabInfo.TabOrder))
                objTabInfo.PortalID = Convert.ToInt32(Null.SetNull(dr("PortalID"), objTabInfo.PortalID))
                objTabInfo.TabName = Convert.ToString(Null.SetNull(dr("TabName"), objTabInfo.TabName))
                objTabInfo.IsVisible = Convert.ToBoolean(Null.SetNull(dr("IsVisible"), objTabInfo.IsVisible))
                objTabInfo.ParentId = Convert.ToInt32(Null.SetNull(dr("ParentId"), objTabInfo.ParentId))
                objTabInfo.Level = Convert.ToInt32(Null.SetNull(dr("Level"), objTabInfo.Level))
                objTabInfo.IconFile = Convert.ToString(Null.SetNull(dr("IconFile"), objTabInfo.IconFile))
                objTabInfo.DisableLink = Convert.ToBoolean(Null.SetNull(dr("DisableLink"), objTabInfo.DisableLink))
                objTabInfo.Title = Convert.ToString(Null.SetNull(dr("Title"), objTabInfo.Title))
                objTabInfo.Description = Convert.ToString(Null.SetNull(dr("Description"), objTabInfo.Description))
                objTabInfo.KeyWords = Convert.ToString(Null.SetNull(dr("KeyWords"), objTabInfo.KeyWords))
                objTabInfo.IsDeleted = Convert.ToBoolean(Null.SetNull(dr("IsDeleted"), objTabInfo.IsDeleted))
                objTabInfo.Url = Convert.ToString(Null.SetNull(dr("Url"), objTabInfo.Url))
                objTabInfo.SkinSrc = Convert.ToString(Null.SetNull(dr("SkinSrc"), objTabInfo.SkinSrc))
                objTabInfo.ContainerSrc = Convert.ToString(Null.SetNull(dr("ContainerSrc"), objTabInfo.ContainerSrc))
                objTabInfo.TabPath = Convert.ToString(Null.SetNull(dr("TabPath"), objTabInfo.TabPath))
                objTabInfo.StartDate = Convert.ToDateTime(Null.SetNull(dr("StartDate"), objTabInfo.StartDate))
                objTabInfo.EndDate = Convert.ToDateTime(Null.SetNull(dr("EndDate"), objTabInfo.EndDate))
                objTabInfo.HasChildren = Convert.ToBoolean(Null.SetNull(dr("HasChildren"), objTabInfo.HasChildren))
                objTabInfo.RefreshInterval = Convert.ToInt32(Null.SetNull(dr("RefreshInterval"), objTabInfo.RefreshInterval))
                objTabInfo.PageHeadText = Convert.ToString(Null.SetNull(dr("PageHeadText"), objTabInfo.PageHeadText))
                objTabInfo.IsSecure = Convert.ToBoolean(Null.SetNull(dr("IsSecure"), objTabInfo.IsSecure))

                If Not objTabInfo Is Nothing Then
                    objTabInfo.TabPermissions = objTabPermissionController.GetTabPermissionsCollectionByTabID(objTabInfo.TabID, objTabInfo.PortalID)
                    objTabInfo.AdministratorRoles = objTabPermissionController.GetTabPermissions(objTabInfo.TabPermissions, "EDIT")
                    objTabInfo.AuthorizedRoles = objTabPermissionController.GetTabPermissions(objTabInfo.TabPermissions, "VIEW")
                    If CheckForLegacyFields Then
                        If objTabInfo.AdministratorRoles = ";" Then
                            ' this code is here for legacy support - the AdministratorRoles were stored as a concatenated list of roleids prior to DNN 3.0
                            Try
                                objTabInfo.AdministratorRoles = Convert.ToString(Null.SetNull(dr("AdministratorRoles"), objTabInfo.AdministratorRoles))
                            Catch
                                ' the AdministratorRoles field was removed from the Tabs table in 3.0
                            End Try
                        End If
                        If objTabInfo.AuthorizedRoles = ";" Then
                            ' this code is here for legacy support - the AuthorizedRoles were stored as a concatenated list of roleids prior to DNN 3.0
                            Try
                                objTabInfo.AuthorizedRoles = Convert.ToString(Null.SetNull(dr("AuthorizedRoles"), objTabInfo.AuthorizedRoles))
                            Catch
                                ' the AuthorizedRoles field was removed from the Tabs table in 3.0
                            End Try
                        End If
                    End If
                End If

                objTabInfo.BreadCrumbs = Nothing
                objTabInfo.Panes = Nothing
                objTabInfo.Modules = Nothing
            Else
                objTabInfo = Nothing
            End If

            Return objTabInfo
        End Function

        Private Function FillTabInfoCollection(ByVal dr As IDataReader, ByVal CheckForLegacyFields As Boolean) As ArrayList

            Dim arr As New ArrayList
            Try
                Dim obj As TabInfo
                While dr.Read
                    ' fill business object
                    obj = FillTabInfo(dr, False, CheckForLegacyFields)
                    ' add to collection
                    arr.Add(obj)
                End While
            Catch exc As Exception
                LogException(exc)
            Finally
                ' close datareader
                If Not dr Is Nothing Then
                    dr.Close()
                End If
            End Try

            Return arr
        End Function

        Private Function FillTabInfoDictionary(ByVal dr As IDataReader) As Dictionary(Of Integer, TabInfo)
            Dim dic As New Dictionary(Of Integer, TabInfo)
            Try
                Dim obj As TabInfo
                While dr.Read
                    ' fill business object
                    obj = FillTabInfo(dr, False, False)
                    ' add to dictionary
                    dic.Add(obj.TabID, obj)
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

        Private Function GetTabByNameAndParent(ByVal TabName As String, ByVal PortalId As Integer, ByVal ParentId As Integer) As TabInfo
            Dim arrTabs As ArrayList = GetTabsByNameAndPortal(TabName, PortalId)
            Dim intTab As Integer = -1

            If Not arrTabs Is Nothing Then
                Select Case arrTabs.Count
                    Case 0    ' no results
                    Case 1    ' exact match
                        intTab = 0
                    Case Else    ' multiple matches
                        Dim intIndex As Integer
                        Dim objTab As TabInfo
                        For intIndex = 0 To arrTabs.Count - 1
                            objTab = CType(arrTabs(intIndex), TabInfo)
                            ' check if the parentids match
                            If objTab.ParentId = ParentId Then
                                intTab = intIndex
                            End If
                        Next intIndex
                        If intTab = -1 Then
                            ' no match - return the first item
                            intTab = 0
                        End If
                End Select
            End If

            If intTab <> -1 Then
                Return CType(arrTabs(intTab), TabInfo)
            Else
                Return Nothing
            End If
        End Function

        Private Function GetTabsByNameAndPortal(ByVal TabName As String, ByVal PortalId As Integer) As ArrayList
            Dim returnTabs As New ArrayList()
            For Each kvp As KeyValuePair(Of Integer, TabInfo) In GetTabsByPortal(PortalId)
                Dim objTab As TabInfo = kvp.Value

                If objTab.TabName = TabName Then
                    returnTabs.Add(objTab)
                End If
            Next
            Return returnTabs
        End Function

        Private Function GetTabsByParent(ByVal ParentId As Integer, ByVal PortalId As Integer) As ArrayList
            Dim childTabs As New ArrayList()
            For Each kvp As KeyValuePair(Of Integer, TabInfo) In GetTabsByPortal(PortalId)
                Dim objTab As TabInfo = kvp.Value

                If objTab.ParentId = ParentId Then
                    childTabs.Add(objTab)
                End If
            Next
            Return childTabs
        End Function

        Private Sub MoveTab(ByVal objDesktopTabs As ArrayList, ByVal intFromIndex As Integer, ByVal intToIndex As Integer, ByVal intNewLevel As Integer, Optional ByVal blnAddChild As Boolean = True)
            Dim intCounter As Integer
            Dim objTab As TabInfo
            Dim blnInsert As Boolean
            Dim intIncrement As Integer

            Dim intOldLevel As Integer = CType(objDesktopTabs(intFromIndex), TabInfo).Level
            If intToIndex <> objDesktopTabs.Count - 1 Then
                blnInsert = True
            End If

            ' clone tab and children from old parent
            Dim objClone As New ArrayList
            intCounter = intFromIndex
            While intCounter <= objDesktopTabs.Count - 1
                If CType(objDesktopTabs(intCounter), TabInfo).TabID = CType(objDesktopTabs(intFromIndex), TabInfo).TabID Or CType(objDesktopTabs(intCounter), TabInfo).Level > intOldLevel Then
                    objClone.Add(objDesktopTabs(intCounter))
                    intCounter += 1
                Else
                    Exit While
                End If
            End While

            ' remove tab and children from old parent
            objDesktopTabs.RemoveRange(intFromIndex, objClone.Count)
            If intToIndex > intFromIndex Then
                intToIndex -= objClone.Count
            End If

            ' add clone to new parent
            If blnInsert Then
                objClone.Reverse()
            End If

            For Each objTab In objClone
                If blnInsert Then
                    objTab.Level += (intNewLevel - intOldLevel)
                    If blnAddChild Then
                        intIncrement = 1
                    Else
                        intIncrement = 0
                    End If
                    objDesktopTabs.Insert(intToIndex + intIncrement, objTab)
                Else
                    objTab.Level += (intNewLevel - intOldLevel)
                    objDesktopTabs.Add(objTab)
                End If
            Next
        End Sub

        ''' -----------------------------------------------------------------------------
        ''' <summary>
        ''' Updates child tabs TabPath field
        ''' </summary>
        ''' <param name="intTabid">ID of the parent tab</param>
        ''' <remarks>
        ''' When a ParentTab is updated this method should be called to 
        ''' ensure that the TabPath of the Child Tabs is consistent with the Parent
        ''' </remarks>
        ''' <history>
        ''' 	[JWhite]	16/11/2004	Created
        ''' </history>
        ''' -----------------------------------------------------------------------------
        Private Sub UpdateChildTabPath(ByVal intTabid As Integer, ByVal portalId As Integer)
            Dim objtab As TabInfo
            Dim arrTabs As ArrayList = GetTabsByParentId(intTabid, portalId)

            For Each objtab In arrTabs
                Dim oldTabPath As String = objtab.TabPath
                objtab.TabPath = GenerateTabPath(objtab.ParentId, objtab.TabName)
                If oldTabPath <> objtab.TabPath Then
                    DataProvider.Instance().UpdateTab(objtab.TabID, objtab.TabName, objtab.IsVisible, objtab.DisableLink, objtab.ParentId, objtab.IconFile, objtab.Title, objtab.Description, objtab.KeyWords, objtab.IsDeleted, objtab.Url, objtab.SkinSrc, objtab.ContainerSrc, objtab.TabPath, objtab.StartDate, objtab.EndDate, objtab.RefreshInterval, objtab.PageHeadText, objtab.IsSecure)
                    UpdateChildTabPath(objtab.TabID, objtab.PortalID)
                End If
            Next

            ClearCache(portalId)
        End Sub

#End Region

#Region "Public Methods"

        Public Function AddTab(ByVal objTab As TabInfo) As Integer
            Return AddTab(objTab, True)
        End Function

        Public Function AddTab(ByVal objTab As TabInfo, ByVal AddAllTabsModules As Boolean) As Integer
            Dim intTabId As Integer

            objTab.TabPath = GenerateTabPath(objTab.ParentId, objTab.TabName)
            intTabId = DataProvider.Instance().AddTab(objTab.PortalID, objTab.TabName, objTab.IsVisible, objTab.DisableLink, objTab.ParentId, objTab.IconFile, objTab.Title, objTab.Description, objTab.KeyWords, objTab.Url, objTab.SkinSrc, objTab.ContainerSrc, objTab.TabPath, objTab.StartDate, objTab.EndDate, objTab.RefreshInterval, objTab.PageHeadText, objTab.IsSecure)

            Dim objTabPermissionController As New Security.Permissions.TabPermissionController

            If Not objTab.TabPermissions Is Nothing Then
                Dim objTabPermissions As Security.Permissions.TabPermissionCollection
                objTabPermissions = objTab.TabPermissions

                Dim objTabPermission As New Security.Permissions.TabPermissionInfo
                For Each objTabPermission In objTabPermissions
                    objTabPermission.TabID = intTabId
                    If objTabPermission.AllowAccess Then
                        objTabPermissionController.AddTabPermission(objTabPermission)
                    End If
                Next
            End If
            If Not Null.IsNull(objTab.PortalID) Then
                UpdatePortalTabOrder(objTab.PortalID, intTabId, objTab.ParentId, 0, 0, objTab.IsVisible, True)
            Else    ' host tab
                Dim arrTabs As ArrayList = GetTabsByParentId(objTab.ParentId, objTab.PortalID)
                objTab.TabID = intTabId
                objTab.TabOrder = (arrTabs.Count * 2) - 1
                objTab.Level = 1
                UpdateTabOrder(objTab)
            End If

            If AddAllTabsModules Then
                Dim objmodules As New ModuleController
                Dim arrMods As ArrayList = objmodules.GetAllTabsModules(objTab.PortalID, True)

                For Each objModule As ModuleInfo In arrMods
                    objmodules.CopyModule(objModule.ModuleID, objModule.TabID, intTabId, "", True)
                Next
            End If

            ClearCache(objTab.PortalID)
            DataCache.RemoveCache(DataCache.PortalDictionaryCacheKey)

            Return intTabId
        End Function

        Public Sub CopyDesignToChildren(ByVal tabs As ArrayList, ByVal skinSrc As String, ByVal containerSrc As String)

            For Each objTab As TabInfo In tabs
                DataProvider.Instance().UpdateTab(objTab.TabID, objTab.TabName, objTab.IsVisible, objTab.DisableLink, objTab.ParentId, objTab.IconFile, objTab.Title, objTab.Description, objTab.KeyWords, objTab.IsDeleted, objTab.Url, skinSrc, containerSrc, objTab.TabPath, objTab.StartDate, objTab.EndDate, objTab.RefreshInterval, objTab.PageHeadText, objTab.IsSecure)
            Next

            If tabs.Count > 0 Then
                DotNetNuke.Common.Utilities.DataCache.ClearTabsCache(CType(tabs(0), TabInfo).PortalID)
            End If

        End Sub

        Public Sub CopyPermissionsToChildren(ByVal tabs As ArrayList, ByVal newPermissions As Permissions.TabPermissionCollection)

            Dim objTabPermissionController As New Security.Permissions.TabPermissionController

            For Each objTab As TabInfo In tabs

                Dim objCurrentTabPermissions As Security.Permissions.TabPermissionCollection
                objCurrentTabPermissions = objTabPermissionController.GetTabPermissionsCollectionByTabID(objTab.TabID, objTab.PortalID)

                If Not objCurrentTabPermissions.CompareTo(newPermissions) Then
                    objTabPermissionController.DeleteTabPermissionsByTabID(objTab.TabID)

                    For Each objTabPermission As Security.Permissions.TabPermissionInfo In newPermissions
                        If objTabPermission.AllowAccess Then
                            objTabPermission.TabID = objTab.TabID
                            objTabPermissionController.AddTabPermission(objTabPermission)
                        End If
                    Next
                End If
            Next

            If tabs.Count > 0 Then
                DotNetNuke.Common.Utilities.DataCache.ClearTabsCache(CType(tabs(0), TabInfo).PortalID)
            End If

        End Sub

        Public Sub CopyTab(ByVal PortalId As Integer, ByVal FromTabId As Integer, ByVal ToTabId As Integer, ByVal IncludeContent As Boolean)
            Dim objModules As New ModuleController
            Dim objModule As ModuleInfo

            For Each kvp As KeyValuePair(Of Integer, ModuleInfo) In objModules.GetTabModules(FromTabId)
                objModule = kvp.Value

                ' if the module shows on all pages does not need to be copied since it will
                ' be already added to this page
                If Not objModule.AllTabs Then
                    If IncludeContent = False Then
                        objModule.ModuleID = Null.NullInteger
                    End If

                    objModule.TabID = ToTabId
                    objModules.AddModule(objModule)
                End If
            Next
        End Sub

        ''' -----------------------------------------------------------------------------
        ''' <summary>
        ''' Deletes a tab premanently from the database
        ''' </summary>
        ''' <param name="TabId">TabId of the tab to be deleted</param>
        ''' <param name="PortalId">PortalId of the portal</param>
        ''' <remarks>
        ''' </remarks>
        ''' <history>
        ''' 	[Vicenç]	19/09/2004	Added skin deassignment before deleting the tab.
        ''' </history>
        ''' -----------------------------------------------------------------------------
        Public Sub DeleteTab(ByVal TabId As Integer, ByVal PortalId As Integer)
            ' parent tabs can not be deleted
            Dim arrTabs As ArrayList = GetTabsByParentId(TabId, PortalId)

            If arrTabs.Count = 0 Then
                DataProvider.Instance().DeleteTab(TabId)
                UpdatePortalTabOrder(PortalId, TabId, -2, 0, 0, True)
            End If

            ClearCache(PortalId)
            DataCache.RemoveCache(DataCache.PortalDictionaryCacheKey)
        End Sub

        Public Function GetAllTabs(ByVal CheckLegacyFields As Boolean) As ArrayList
            Return FillTabInfoCollection(DataProvider.Instance().GetAllTabs(), CheckLegacyFields)
        End Function

        Public Function GetAllTabs() As ArrayList
            Return GetAllTabs(True)
        End Function

        Public Function GetTab(ByVal TabId As Integer, ByVal PortalId As Integer, ByVal ignoreCache As Boolean) As TabInfo
            Dim tab As TabInfo = Nothing
            Dim bFound As Boolean = False

            ' if we are using the cache
            If Not ignoreCache Then
                ' if we do not know the PortalId then try to find it in the Portals Dictionary using the TabId
                If Null.IsNull(PortalId) Then
                    Dim portalDic As Dictionary(Of Integer, Integer) = PortalController.GetPortalDictionary()
                    If portalDic.ContainsKey(TabId) Then
                        PortalId = portalDic(TabId)
                    End If
                End If
                ' if we have the PortalId then try to get the TabInfo object from the Tabs Dictionary
                If Not Null.IsNull(PortalId) Then
                    Dim dicTabs As Dictionary(Of Integer, TabInfo)
                    dicTabs = GetTabsByPortal(PortalId)
                    bFound = dicTabs.TryGetValue(TabId, tab)
                End If
            End If

            ' if we are not using the cache or did not find the TabInfo object in the cache
            If ignoreCache Or Not bFound Then
                ' get the TabInfo object from the database
                Dim dr As IDataReader = DataProvider.Instance().GetTab(TabId)
                Try
                    tab = FillTabInfo(dr)
                Finally
                    If Not dr Is Nothing Then
                        dr.Close()
                    End If
                End Try
            End If

            Return tab
        End Function

        Public Function GetTabByName(ByVal TabName As String, ByVal PortalId As Integer) As TabInfo
            Return GetTabByNameAndParent(TabName, PortalId, Integer.MinValue)
        End Function

        Public Function GetTabByName(ByVal TabName As String, ByVal PortalId As Integer, ByVal ParentId As Integer) As TabInfo
            Return GetTabByNameAndParent(TabName, PortalId, ParentId)
        End Function

        Public Function GetTabCount(ByVal portalId As Integer) As Integer
            Return DataProvider.Instance().GetTabCount(portalId)
        End Function

        Public Function GetTabs(ByVal PortalId As Integer) As ArrayList
            Dim arrTabs As New ArrayList
            For Each tabPair As KeyValuePair(Of Integer, TabInfo) In GetTabsByPortal(PortalId)
                arrTabs.Add(tabPair.Value)
            Next
            Return arrTabs
        End Function

        Public Function GetTabsByParentId(ByVal ParentId As Integer, ByVal PortalId As Integer) As ArrayList
            Return GetTabsByParent(ParentId, PortalId)
        End Function

        Public Function GetTabsByPortal(ByVal PortalId As Integer) As Dictionary(Of Integer, TabInfo)
            Dim key As String = String.Format(DataCache.TabCacheKey, PortalId)

            'First Check the Tab Cache
            Dim tabs As Dictionary(Of Integer, TabInfo) = TryCast(DataCache.GetPersistentCacheItem(key, GetType(Dictionary(Of Integer, TabInfo))), Dictionary(Of Integer, TabInfo))

            If tabs Is Nothing OrElse tabs.Count = 0 Then
                'tab caching settings
                Dim timeOut As Int32 = DataCache.TabCacheTimeOut * Convert.ToInt32(Common.Globals.PerformanceSetting)

                'Get tabs form Database
                tabs = FillTabInfoDictionary(DataProvider.Instance().GetTabs(PortalId))

                'Cache tabs
                If timeOut > 0 Then
                    DataCache.SetCache(key, tabs, TimeSpan.FromMinutes(timeOut), True)
                End If
            End If

            Return tabs
        End Function

        Public Sub UpdatePortalTabOrder(ByVal PortalId As Integer, ByVal TabId As Integer, ByVal NewParentId As Integer, ByVal Level As Integer, ByVal Order As Integer, ByVal IsVisible As Boolean, Optional ByVal NewTab As Boolean = False)
            Dim objTab As TabInfo
            Dim intCounter As Integer = 0
            Dim intFromIndex As Integer = -1
            Dim intOldParentId As Integer = -2
            Dim intToIndex As Integer = -1
            Dim intNewParentIndex As Integer = 0
            Dim intLevel As Integer
            Dim intAddTabLevel As Integer

            Dim objPortals As New PortalController
            Dim objPortal As PortalInfo = objPortals.GetPortal(PortalId)

            'hashtable to prevent db calls when no change
            Dim htabs As New Hashtable

            ' create temporary tab collection
            Dim objTabs As New ArrayList

            ' populate temporary tab collection
            For Each tabPair As KeyValuePair(Of Integer, TabInfo) In GetTabsByPortal(PortalId)
                objTab = tabPair.Value

                If NewTab = False Or objTab.TabID <> TabId Then
                    ' save old data
                    htabs.Add(objTab.TabID, New TabOrderHelper(objTab.TabOrder, objTab.Level, objTab.ParentId))

                    If objTab.TabOrder = 0 Then
                        objTab.TabOrder = 999
                    End If
                    objTabs.Add(objTab)
                    If objTab.TabID = TabId Then
                        intOldParentId = objTab.ParentId
                        intFromIndex = intCounter
                    End If
                    If objTab.TabID = NewParentId Then
                        intNewParentIndex = intCounter
                        intAddTabLevel = objTab.Level + 1
                    End If
                    intCounter += 1
                End If
            Next

            If NewParentId <> -2 Then    ' not deleted
                ' adding new tab
                If intFromIndex = -1 Then
                    'Tab exists - just not in Cache yet so fetch from db , to ensure we have all the properties set
                    objTab = GetTab(TabId, PortalId, True)
                    objTab.TabID = TabId
                    objTab.ParentId = NewParentId
                    objTab.IsVisible = IsVisible
                    objTab.Level = intAddTabLevel
                    objTabs.Add(objTab)
                    intFromIndex = objTabs.Count - 1
                End If

                If Level = 0 And Order = 0 Then
                    CType(objTabs(intFromIndex), TabInfo).IsVisible = IsVisible
                End If
            End If

            If NewParentId <> -2 Then    ' not deleted
                ' if the parent changed or we added a new non root level tab
                If intOldParentId <> NewParentId And Not (intOldParentId = -2 And NewParentId = -1) Then
                    ' locate position of last child for new parent
                    If NewParentId <> -1 Then
                        intLevel = CType(objTabs(intNewParentIndex), TabInfo).Level
                    Else
                        intLevel = -1
                    End If

                    intCounter = intNewParentIndex + 1
                    While intCounter <= objTabs.Count - 1
                        If CType(objTabs(intCounter), TabInfo).Level > intLevel Then
                            intToIndex = intCounter
                        Else
                            Exit While
                        End If
                        intCounter += 1
                    End While
                    ' adding to parent with no children
                    If intToIndex = -1 Then
                        intToIndex = intNewParentIndex
                    End If
                    ' move tab
                    CType(objTabs(intFromIndex), TabInfo).ParentId = NewParentId
                    MoveTab(objTabs, intFromIndex, intToIndex, intLevel + 1)
                Else
                    ' if level has changed
                    If Level <> 0 Then
                        intLevel = CType(objTabs(intFromIndex), TabInfo).Level

                        Dim blnValid As Boolean = True
                        Select Case Level
                            Case -1
                                If intLevel = 0 Then
                                    blnValid = False
                                End If
                            Case 1
                                If intFromIndex > 0 Then
                                    If intLevel > CType(objTabs(intFromIndex - 1), TabInfo).Level Then
                                        blnValid = False
                                    End If
                                Else
                                    blnValid = False
                                End If
                        End Select

                        If blnValid Then
                            Dim intNewLevel As Integer
                            If Level = -1 Then
                                intNewLevel = intLevel + Level
                            Else
                                intNewLevel = intLevel
                            End If

                            ' get new parent
                            NewParentId = -2
                            intCounter = intFromIndex - 1
                            While intCounter >= 0 And NewParentId = -2
                                objTab = CType(objTabs(intCounter), TabInfo)
                                If objTab.Level = intNewLevel And Not objTab.IsDeleted Then
                                    If Level = -1 Then
                                        NewParentId = objTab.ParentId
                                    Else
                                        NewParentId = objTab.TabID
                                    End If
                                    intNewParentIndex = intCounter
                                End If
                                intCounter -= 1
                            End While
                            CType(objTabs(intFromIndex), TabInfo).ParentId = NewParentId

                            If Level = -1 Then
                                ' locate position of next child for parent
                                intToIndex = -1
                                intCounter = intNewParentIndex + 1
                                While intCounter <= objTabs.Count - 1
                                    If CType(objTabs(intCounter), TabInfo).Level > intNewLevel Then
                                        intToIndex = intCounter
                                    Else
                                        Exit While
                                    End If
                                    intCounter += 1
                                End While
                                ' adding to parent with no children
                                If intToIndex = -1 Then
                                    intToIndex = intNewParentIndex
                                End If
                            Else
                                intToIndex = intFromIndex - 1
                                intNewLevel = intLevel + Level
                            End If

                            ' move tab
                            If intFromIndex = intToIndex Then
                                CType(objTabs(intFromIndex), TabInfo).Level = intNewLevel
                            Else
                                MoveTab(objTabs, intFromIndex, intToIndex, intNewLevel)
                            End If
                        End If
                    Else
                        ' if order has changed
                        If Order <> 0 Then
                            intLevel = CType(objTabs(intFromIndex), TabInfo).Level

                            ' find previous/next item for parent
                            intToIndex = -1
                            intCounter = intFromIndex + Order
                            While intCounter >= 0 And intCounter <= objTabs.Count - 1 And intToIndex = -1
                                objTab = CType(objTabs(intCounter), TabInfo)
                                If objTab.ParentId = NewParentId And Not objTab.IsDeleted Then
                                    intToIndex = intCounter
                                End If
                                intCounter += Order
                            End While
                            If intToIndex <> -1 Then
                                If Order = 1 Then
                                    ' locate position of next child for parent
                                    intNewParentIndex = intToIndex
                                    intToIndex = -1
                                    intCounter = intNewParentIndex + 1
                                    While intCounter <= objTabs.Count - 1
                                        If CType(objTabs(intCounter), TabInfo).Level > intLevel Then
                                            intToIndex = intCounter
                                        Else
                                            Exit While
                                        End If
                                        intCounter += 1
                                    End While
                                    ' adding to parent with no children
                                    If intToIndex = -1 Then
                                        intToIndex = intNewParentIndex
                                    End If
                                    intToIndex += 1
                                End If
                                MoveTab(objTabs, intFromIndex, intToIndex, intLevel, False)
                            End If
                        End If
                    End If
                End If
            End If

            ' update the tabs
            Dim intDesktopTabOrder As Integer = -1
            Dim intAdminTabOrder As Integer = 9999    ' this seeds the taborder for the admin tab so that they are always at the end of the tab list ( max = 5000 desktop tabs per portal )
            For Each objTab In objTabs
                If ((objTab.TabID = objPortal.AdminTabId) Or (objTab.ParentId = objPortal.AdminTabId) Or (objTab.TabID = objPortal.SuperTabId) Or (objTab.ParentId = objPortal.SuperTabId)) And _
                 (objPortal.AdminTabId <> -1) Then    ' special case when creating new portals
                    intAdminTabOrder += 2
                    objTab.TabOrder = intAdminTabOrder
                Else    ' desktop tab
                    intDesktopTabOrder += 2
                    objTab.TabOrder = intDesktopTabOrder
                End If
                ' update only if changed
                If htabs.Contains(objTab.TabID) Then
                    Dim ttab As TabOrderHelper = CType(htabs(objTab.TabID), TabOrderHelper)
                    If objTab.TabOrder <> ttab.TabOrder Or objTab.Level <> ttab.Level Or objTab.ParentId <> ttab.ParentId Then
                        UpdateTabOrder(objTab)
                    End If
                Else
                    UpdateTabOrder(objTab)
                End If
            Next

            'clear Tabs cache
            ClearCache(PortalId)
        End Sub

        Public Sub UpdateTab(ByVal objTab As TabInfo)
            Dim updateChildren As Boolean = False
            Dim objTmpTab As TabInfo = GetTab(objTab.TabID, objTab.PortalID, False)
            If objTmpTab.TabName <> objTab.TabName Or objTmpTab.ParentId <> objTab.ParentId Then
                updateChildren = True
            End If

            UpdatePortalTabOrder(objTab.PortalID, objTab.TabID, objTab.ParentId, 0, 0, objTab.IsVisible)

            DataProvider.Instance().UpdateTab(objTab.TabID, objTab.TabName, objTab.IsVisible, objTab.DisableLink, objTab.ParentId, objTab.IconFile, objTab.Title, objTab.Description, objTab.KeyWords, objTab.IsDeleted, objTab.Url, objTab.SkinSrc, objTab.ContainerSrc, objTab.TabPath, objTab.StartDate, objTab.EndDate, objTab.RefreshInterval, objTab.PageHeadText, objTab.IsSecure)

            Dim objTabPermissionController As New Security.Permissions.TabPermissionController

            Dim objTabPermissions As Security.Permissions.TabPermissionCollection
            objTabPermissions = objTab.TabPermissions

            Dim objCurrentTabPermissions As Security.Permissions.TabPermissionCollection
            objCurrentTabPermissions = objTabPermissionController.GetTabPermissionsCollectionByTabID(objTab.TabID, objTab.PortalID)

            If Not objCurrentTabPermissions.CompareTo(objTab.TabPermissions) Then
                objTabPermissionController.DeleteTabPermissionsByTabID(objTab.TabID)

                Dim objTabPermission As New Security.Permissions.TabPermissionInfo
                For Each objTabPermission In objTabPermissions
                    If objTabPermission.AllowAccess Then
                        objTabPermissionController.AddTabPermission(objTabPermission)
                    End If
                Next
            End If
            If updateChildren Then
                UpdateChildTabPath(objTab.TabID, objTab.PortalID)
            End If

            ClearCache(objTab.PortalID)
        End Sub

        Public Sub UpdateTabOrder(ByVal objTab As TabInfo)
            objTab.TabPath = GenerateTabPath(objTab.ParentId, objTab.TabName)
            DataProvider.Instance().UpdateTabOrder(objTab.TabID, objTab.TabOrder, objTab.Level, objTab.ParentId, objTab.TabPath)
        End Sub

#End Region

#Region "Private Shared Methods"

        Private Shared Function DeleteChildTabs(ByVal intTabid As Integer, ByVal PortalSettings As PortalSettings, ByVal UserId As Integer) As Boolean
            Dim objtabs As New TabController
            Dim objtab As TabInfo
            Dim arrTabs As ArrayList = objtabs.GetTabsByParentId(intTabid, PortalSettings.PortalId)

            Dim bDeleted As Boolean = True

            For Each objtab In arrTabs
                If objtab.TabID <> PortalSettings.AdminTabId And objtab.TabID <> PortalSettings.SplashTabId And objtab.TabID <> PortalSettings.HomeTabId And objtab.TabID <> PortalSettings.LoginTabId And objtab.TabID <> PortalSettings.UserTabId Then
                    'delete child tabs
                    If DeleteChildTabs(objtab.TabID, PortalSettings, UserId) Then
                        objtab.IsDeleted = True
                        objtabs.UpdateTab(objtab)

                        Dim objEventLog As New Services.Log.EventLog.EventLogController
                        objEventLog.AddLog(objtab, PortalSettings, UserId, "", Services.Log.EventLog.EventLogController.EventLogType.TAB_SENT_TO_RECYCLE_BIN)
                    Else
                        'cannot delete tab, stop deleting and exit
                        bDeleted = False
                        Exit For
                    End If
                Else
                    'cannot delete tab, stop deleting and exit
                    bDeleted = False
                    Exit For
                End If
            Next

            Return bDeleted

        End Function

        ''' -----------------------------------------------------------------------------
        ''' <summary>
        ''' Deserializes tab permissions
        ''' </summary>
        ''' <param name="nodeTabPermissions">Node for tab permissions</param>
        ''' <param name="objTab">Tab being processed</param>
        ''' <param name="IsAdminTemplate">Flag to indicate if we are parsing admin template</param>
        ''' <returns></returns>
        ''' <remarks>
        ''' </remarks>
        ''' <history>
        ''' 	[Vicenç]	15/10/2004	Created
        '''     [cnurse]    10/02/2007  Moved from PortalController
        ''' </history>
        ''' -----------------------------------------------------------------------------
        Private Shared Function DeserializeTabPermissions(ByVal nodeTabPermissions As XmlNodeList, ByVal objTab As TabInfo, ByVal IsAdminTemplate As Boolean) As Security.Permissions.TabPermissionCollection
            Dim objTabPermissions As New Security.Permissions.TabPermissionCollection
            Dim objPermissionController As New Security.Permissions.PermissionController
            Dim objPermission As Security.Permissions.PermissionInfo
            Dim objTabPermission As Security.Permissions.TabPermissionInfo
            Dim objRoleController As New RoleController
            Dim objRole As RoleInfo
            Dim RoleID As Integer
            Dim PermissionID As Integer
            Dim PermissionKey, PermissionCode As String
            Dim RoleName As String
            Dim AllowAccess As Boolean
            Dim arrPermissions As ArrayList
            Dim i As Integer
            Dim xmlTabPermission As XmlNode

            For Each xmlTabPermission In nodeTabPermissions
                PermissionKey = XmlUtils.GetNodeValue(xmlTabPermission, "permissionkey")
                PermissionCode = XmlUtils.GetNodeValue(xmlTabPermission, "permissioncode")
                RoleName = XmlUtils.GetNodeValue(xmlTabPermission, "rolename")
                AllowAccess = XmlUtils.GetNodeValueBoolean(xmlTabPermission, "allowaccess")
                arrPermissions = objPermissionController.GetPermissionByCodeAndKey(PermissionCode, PermissionKey)

                For i = 0 To arrPermissions.Count - 1
                    objPermission = CType(arrPermissions(i), Security.Permissions.PermissionInfo)
                    PermissionID = objPermission.PermissionID
                Next
                RoleID = Integer.MinValue
                Select Case RoleName
                    Case glbRoleAllUsersName
                        RoleID = Convert.ToInt32(glbRoleAllUsers)
                    Case Common.Globals.glbRoleUnauthUserName
                        RoleID = Convert.ToInt32(glbRoleUnauthUser)
                    Case Else
                        Dim objPortals As New PortalController
                        Dim objPortal As PortalInfo = objPortals.GetPortal(objTab.PortalID)
                        objRole = objRoleController.GetRoleByName(objPortal.PortalID, RoleName)
                        If Not objRole Is Nothing Then
                            RoleID = objRole.RoleID
                        Else
                            ' if parsing admin.template and role administrators redefined, use portal.administratorroleid
                            If IsAdminTemplate AndAlso RoleName.ToLower() = "administrators" Then
                                RoleID = objPortal.AdministratorRoleId
                            End If
                        End If
                End Select

                ' if role was found add, otherwise ignore
                If RoleID <> Integer.MinValue Then
                    objTabPermission = New Security.Permissions.TabPermissionInfo
                    objTabPermission.TabID = objTab.TabID
                    objTabPermission.PermissionID = PermissionID
                    objTabPermission.RoleID = RoleID
                    objTabPermission.AllowAccess = AllowAccess
                    objTabPermissions.Add(objTabPermission)
                End If
            Next
            Return objTabPermissions
        End Function

#End Region

#Region "Public Shared Methods"

        Public Shared Function DeleteTab(ByVal tabId As Integer, ByVal PortalSettings As PortalSettings, ByVal UserId As Integer) As Boolean
            Dim bDeleted As Boolean = True

            If tabId <> PortalSettings.AdminTabId And tabId <> PortalSettings.SplashTabId And tabId <> PortalSettings.HomeTabId And tabId <> PortalSettings.LoginTabId And tabId <> PortalSettings.UserTabId Then
                Dim objTabs As New TabController
                Dim tabs As ArrayList = GetPortalTabs(PortalSettings.DesktopTabs, tabId, False, False, False, False, False)

                If tabs.Count > 0 Then
                    Dim objTab As TabInfo = objTabs.GetTab(tabId, PortalSettings.PortalId, False)
                    If Not objTab Is Nothing Then
                        'delete child tabs
                        If DeleteChildTabs(objTab.TabID, PortalSettings, UserId) Then
                            objTab.IsDeleted = True
                            objTabs.UpdateTab(objTab)

                            Dim objEventLog As New Services.Log.EventLog.EventLogController
                            objEventLog.AddLog(objTab, PortalSettings, UserId, "", Services.Log.EventLog.EventLogController.EventLogType.TAB_SENT_TO_RECYCLE_BIN)
                        Else
                            bDeleted = False
                        End If
                    End If
                Else
                    bDeleted = False
                End If
            Else
                bDeleted = False
            End If

            Return bDeleted
        End Function

        ''' -----------------------------------------------------------------------------
        ''' <summary>
        ''' Processes all panes and modules in the template file
        ''' </summary>
        ''' <param name="nodePanes">Template file node for the panes is current tab</param>
        ''' <param name="PortalId">PortalId of the new portal</param>
        ''' <param name="TabId">Tab being processed</param>
        ''' <remarks>
        ''' </remarks>
        ''' <history>
        ''' 	[VMasanas]	03/09/2004	Created
        ''' 	[VMasanas]	15/10/2004	Modified for new skin structure
        '''		[cnurse]	15/10/2004	Modified to allow for merging template
        '''								with existing pages
        '''     [cnurse]    10/02/2007  Moved from PortalController
        ''' </history>
        ''' -----------------------------------------------------------------------------
        Public Shared Sub DeserializePanes(ByVal nodePanes As XmlNode, ByVal PortalId As Integer, ByVal TabId As Integer, ByVal mergeTabs As PortalTemplateModuleAction, ByVal hModules As Hashtable)
            Dim objModules As New ModuleController
            Dim objModule As ModuleInfo

            Dim dicModules As Dictionary(Of Integer, ModuleInfo) = objModules.GetTabModules(TabId)

            'If Mode is Replace remove all the modules already on this Tab
            If mergeTabs = PortalTemplateModuleAction.Replace Then
                For Each kvp As KeyValuePair(Of Integer, ModuleInfo) In dicModules
                    objModule = kvp.Value
                    objModules.DeleteTabModule(TabId, objModule.ModuleID)
                Next
            End If

            ' iterate through the panes
            For Each nodePane As XmlNode In nodePanes.ChildNodes
                ' iterate through the modules
                If Not nodePane.SelectSingleNode("modules") Is Nothing Then
                    For Each nodeModule As XmlNode In nodePane.SelectSingleNode("modules")
                        ModuleController.DeserializeModule(nodeModule, nodePane, PortalId, TabId, mergeTabs, hModules)
                    Next
                End If
            Next
        End Sub

        Public Shared Function DeserializeTab(ByVal tabName As String, ByVal nodeTab As XmlNode, ByVal PortalId As Integer) As TabInfo
            Return TabController.DeserializeTab(tabName, nodeTab, Nothing, New Hashtable(), PortalId, False, PortalTemplateModuleAction.Ignore, New Hashtable())
        End Function

        Public Shared Function DeserializeTab(ByVal nodeTab As XmlNode, ByVal objTab As TabInfo, ByVal PortalId As Integer) As TabInfo
            Return TabController.DeserializeTab(nodeTab, objTab, New Hashtable(), PortalId, False, PortalTemplateModuleAction.Ignore, New Hashtable())
        End Function

        Public Shared Function DeserializeTab(ByVal nodeTab As XmlNode, ByVal objTab As TabInfo, ByVal PortalId As Integer, ByVal mergeTabs As PortalTemplateModuleAction) As TabInfo
            Return TabController.DeserializeTab(nodeTab, objTab, New Hashtable(), PortalId, False, mergeTabs, New Hashtable())
        End Function

        Public Shared Function DeserializeTab(ByVal nodeTab As XmlNode, ByVal objTab As TabInfo, ByVal hTabs As Hashtable, ByVal PortalId As Integer, ByVal IsAdminTemplate As Boolean, ByVal mergeTabs As PortalTemplateModuleAction, ByVal hModules As Hashtable) As TabInfo
            Return TabController.DeserializeTab(XmlUtils.GetNodeValue(nodeTab, "name"), nodeTab, objTab, hTabs, PortalId, IsAdminTemplate, mergeTabs, hModules)
        End Function

        Public Shared Function DeserializeTab(ByVal tabName As String, ByVal nodeTab As XmlNode, ByVal objTab As TabInfo, ByVal hTabs As Hashtable, ByVal PortalId As Integer, ByVal IsAdminTemplate As Boolean, ByVal mergeTabs As PortalTemplateModuleAction, ByVal hModules As Hashtable) As TabInfo
            Dim objTabs As New TabController

            If tabName <> "" Then
                If objTab Is Nothing Then
                    objTab = New TabInfo
                    objTab.TabID = Null.NullInteger
                    objTab.ParentId = Null.NullInteger
                    objTab.TabName = tabName
                End If
                objTab.PortalID = PortalId
                objTab.Title = XmlUtils.GetNodeValue(nodeTab, "title")
                objTab.Description = XmlUtils.GetNodeValue(nodeTab, "description")
                objTab.KeyWords = XmlUtils.GetNodeValue(nodeTab, "keywords")
                objTab.IsVisible = XmlUtils.GetNodeValueBoolean(nodeTab, "visible", True)
                objTab.DisableLink = XmlUtils.GetNodeValueBoolean(nodeTab, "disabled")
                objTab.IconFile = ImportFile(PortalId, XmlUtils.GetNodeValue(nodeTab, "iconfile"))
                objTab.Url = XmlUtils.GetNodeValue(nodeTab, "url")
                objTab.StartDate = XmlUtils.GetNodeValueDate(nodeTab, "startdate", Null.NullDate)
                objTab.EndDate = XmlUtils.GetNodeValueDate(nodeTab, "enddate", Null.NullDate)
                objTab.RefreshInterval = XmlUtils.GetNodeValueInt(nodeTab, "refreshinterval", Null.NullInteger)
                objTab.PageHeadText = XmlUtils.GetNodeValue(nodeTab, "pageheadtext", Null.NullString)
                objTab.IsSecure = XmlUtils.GetNodeValueBoolean(nodeTab, "issecure", False)

                objTab.TabPermissions = DeserializeTabPermissions(nodeTab.SelectNodes("tabpermissions/permission"), objTab, IsAdminTemplate)

                ' set tab skin and container
                If XmlUtils.GetNodeValue(nodeTab, "skinsrc", "") <> "" Then
                    objTab.SkinSrc = XmlUtils.GetNodeValue(nodeTab, "skinsrc", "")
                End If
                If XmlUtils.GetNodeValue(nodeTab, "containersrc", "") <> "" Then
                    objTab.ContainerSrc = XmlUtils.GetNodeValue(nodeTab, "containersrc", "")
                End If

                tabName = objTab.TabName
                If XmlUtils.GetNodeValue(nodeTab, "parent") <> "" Then
                    If Not hTabs(XmlUtils.GetNodeValue(nodeTab, "parent")) Is Nothing Then
                        ' parent node specifies the path (tab1/tab2/tab3), use saved tabid
                        objTab.ParentId = Convert.ToInt32(hTabs(XmlUtils.GetNodeValue(nodeTab, "parent")))
                        tabName = XmlUtils.GetNodeValue(nodeTab, "parent") + "/" + objTab.TabName
                    Else
                        ' Parent node doesn't spcecify the path, search by name.
                        ' Possible incoherence if tabname not unique
                        Dim objParent As TabInfo = objTabs.GetTabByName(XmlUtils.GetNodeValue(nodeTab, "parent"), PortalId)
                        If Not objParent Is Nothing Then
                            objTab.ParentId = objParent.TabID
                            tabName = objParent.TabName + "/" + objTab.TabName
                        Else
                            ' parent tab not found!
                            objTab.ParentId = Null.NullInteger
                            tabName = objTab.TabName
                        End If
                    End If
                End If

                ' create/update tab
                If objTab.TabID = Null.NullInteger Then
                    objTab.TabID = objTabs.AddTab(objTab)
                Else
                    objTabs.UpdateTab(objTab)
                End If

                ' extra check for duplicate tabs in same level
                If hTabs(tabName) Is Nothing Then
                    hTabs.Add(tabName, objTab.TabID)
                End If
            End If

            'Parse Panes
            If Not nodeTab.SelectSingleNode("panes") Is Nothing Then
                DeserializePanes(nodeTab.SelectSingleNode("panes"), PortalId, objTab.TabID, mergeTabs, hModules)
            End If

            Return objTab
        End Function

        Public Shared Function GetTabByTabPath(ByVal portalId As Integer, ByVal tabPath As String) As Integer
            Dim strKey As String = "//" & portalId.ToString & tabPath
            Dim tabpathDic As Dictionary(Of String, Integer) = GetTabPathDictionary()
            If tabpathDic.ContainsKey(strKey) Then
                Return tabpathDic(strKey)
            Else
                Return -1
            End If
        End Function

        Public Shared Function GetTabPathDictionary() As Dictionary(Of String, Integer)

            Dim tabpathDic As Dictionary(Of String, Integer) = TryCast(DataCache.GetCache(DataCache.TabPathCacheKey), Dictionary(Of String, Integer))
            If tabpathDic Is Nothing Then
                tabpathDic = New Dictionary(Of String, Integer)(StringComparer.CurrentCultureIgnoreCase)

                Dim objTabController As New TabController
                Dim arrTabs As ArrayList = objTabController.GetAllTabs(False)
                For Each objTab As TabInfo In arrTabs
                    ' add the TabPath and TabId to the TabPath Dictionary
                    If Not objTab.IsDeleted Then
                        Dim strKey As String = "//" & objTab.PortalID.ToString & objTab.TabPath
                        If Not tabpathDic.ContainsKey(strKey) Then
                            tabpathDic(strKey) = objTab.TabID
                        End If
                    End If
                Next
                DataCache.SetCache(DataCache.TabPathCacheKey, tabpathDic, False)
            End If

            Return tabpathDic
        End Function

        ''' <summary>
        ''' SerializeTab
        ''' </summary>
        ''' <param name="xmlTab">The Xml Document to use for the Tab</param>
        ''' <param name="objTab">The TabInfo object to serialize</param>
        ''' <param name="includeAllModules">A flag used to determine if Modules configured to display on all tabs are included</param>
        ''' <param name="includeContent">A flag used to determine if the Module content is included</param>
        Public Shared Function SerializeTab(ByVal xmlTab As XmlDocument, ByVal objTab As TabInfo, ByVal includeAllModules As Boolean, ByVal includeContent As Boolean) As XmlNode
            Return SerializeTab(xmlTab, Nothing, objTab, Nothing, includeAllModules, includeContent)
        End Function

        ''' <summary>
        ''' SerializeTab
        ''' </summary>
        ''' <param name="xmlTab">The Xml Document to use for the Tab</param>
        ''' <param name="hTabs">A Hashtable used to store the names of the tabs</param>
        ''' <param name="objTab">The TabInfo object to serialize</param>
        ''' <param name="objPortal">The Portal object to which the tab belongs</param>
        ''' <param name="includeContent">A flag used to determine if the Module content is included</param>
        Public Shared Function SerializeTab(ByVal xmlTab As XmlDocument, ByVal hTabs As Hashtable, ByVal objTab As TabInfo, ByVal objPortal As PortalInfo, ByVal includeContent As Boolean) As XmlNode
            Return SerializeTab(xmlTab, hTabs, objTab, objPortal, True, includeContent)
        End Function

        ''' <summary>
        ''' SerializeTab
        ''' </summary>
        ''' <param name="xmlTab">The Xml Document to use for the Tab</param>
        ''' <param name="hTabs">A Hashtable used to store the names of the tabs</param>
        ''' <param name="objTab">The TabInfo object to serialize</param>
        ''' <param name="objPortal">The Portal object to which the tab belongs</param>
        ''' <param name="includeAllModules">A flag used to determine if Modules configured to display on all tabs are included</param>
        ''' <param name="includeContent">A flag used to determine if the Module content is included</param>
        Public Shared Function SerializeTab(ByVal xmlTab As XmlDocument, ByVal hTabs As Hashtable, ByVal objTab As TabInfo, ByVal objPortal As PortalInfo, ByVal includeAllModules As Boolean, ByVal includeContent As Boolean) As XmlNode
            Dim xserTabs As New XmlSerializer(GetType(TabInfo))
            Dim sw As New StringWriter
            Dim nodeTab, newnode As XmlNode

            xserTabs.Serialize(sw, objTab)

            xmlTab.LoadXml(sw.GetStringBuilder().ToString())
            nodeTab = xmlTab.SelectSingleNode("tab")
            nodeTab.Attributes.Remove(nodeTab.Attributes.ItemOf("xmlns:xsd"))
            nodeTab.Attributes.Remove(nodeTab.Attributes.ItemOf("xmlns:xsi"))

            'remove unwanted elements
            nodeTab.RemoveChild(nodeTab.SelectSingleNode("tabid"))
            nodeTab.RemoveChild(nodeTab.SelectSingleNode("taborder"))
            nodeTab.RemoveChild(nodeTab.SelectSingleNode("portalid"))
            nodeTab.RemoveChild(nodeTab.SelectSingleNode("parentid"))
            nodeTab.RemoveChild(nodeTab.SelectSingleNode("isdeleted"))
            nodeTab.RemoveChild(nodeTab.SelectSingleNode("tabpath"))
            nodeTab.RemoveChild(nodeTab.SelectSingleNode("haschildren"))
            nodeTab.RemoveChild(nodeTab.SelectSingleNode("authorizedroles"))
            nodeTab.RemoveChild(nodeTab.SelectSingleNode("administratorroles"))

            For Each nodePermission As XmlNode In nodeTab.SelectNodes("tabpermissions/permission")
                nodePermission.RemoveChild(nodePermission.SelectSingleNode("tabpermissionid"))
                nodePermission.RemoveChild(nodePermission.SelectSingleNode("permissionid"))
                nodePermission.RemoveChild(nodePermission.SelectSingleNode("tabid"))
                nodePermission.RemoveChild(nodePermission.SelectSingleNode("roleid"))
                nodePermission.RemoveChild(nodePermission.SelectSingleNode("userid"))
                nodePermission.RemoveChild(nodePermission.SelectSingleNode("username"))
                nodePermission.RemoveChild(nodePermission.SelectSingleNode("displayname"))
            Next

            If objPortal IsNot Nothing Then
                Select Case objTab.TabID
                    Case objPortal.SplashTabId
                        newnode = xmlTab.CreateElement("tabtype")
                        newnode.InnerXml = "splashtab"
                        nodeTab.AppendChild(newnode)
                    Case objPortal.HomeTabId
                        newnode = xmlTab.CreateElement("tabtype")
                        newnode.InnerXml = "hometab"
                        nodeTab.AppendChild(newnode)
                    Case objPortal.UserTabId
                        newnode = xmlTab.CreateElement("tabtype")
                        newnode.InnerXml = "usertab"
                        nodeTab.AppendChild(newnode)
                    Case objPortal.LoginTabId
                        newnode = xmlTab.CreateElement("tabtype")
                        newnode.InnerXml = "logintab"
                        nodeTab.AppendChild(newnode)
                End Select
            End If

            'Manage Parent Tab
            If hTabs IsNot Nothing Then
                If Not Null.IsNull(objTab.ParentId) Then
                    newnode = xmlTab.CreateElement("parent")
                    newnode.InnerXml = HttpContext.Current.Server.HtmlEncode(hTabs(objTab.ParentId).ToString)
                    nodeTab.AppendChild(newnode)

                    ' save tab as: ParentTabName/CurrentTabName
                    hTabs.Add(objTab.TabID, hTabs(objTab.ParentId).ToString + "/" + objTab.TabName)
                Else
                    ' save tab as: CurrentTabName
                    hTabs.Add(objTab.TabID, objTab.TabName)
                End If
            End If

            Dim nodePanes, nodePane, nodeName, nodeModules, nodeModule As XmlNode
            Dim xmlModule As XmlDocument
            Dim objmodule As ModuleInfo
            Dim objmodules As New ModuleController

            ' Serialize modules
            nodePanes = nodeTab.AppendChild(xmlTab.CreateElement("panes"))

            For Each kvp As KeyValuePair(Of Integer, ModuleInfo) In objmodules.GetTabModules(objTab.TabID)
                objmodule = kvp.Value
                If (Not objmodule.IsDeleted) AndAlso (Not objmodule.AllTabs OrElse includeAllModules) Then
                    xmlModule = New XmlDocument
                    nodeModule = ModuleController.SerializeModule(xmlModule, objmodule, includeContent)

                    If nodePanes.SelectSingleNode("descendant::pane[name='" & objmodule.PaneName & "']") Is Nothing Then
                        ' new pane found
                        nodePane = xmlModule.CreateElement("pane")
                        nodeName = nodePane.AppendChild(xmlModule.CreateElement("name"))
                        nodeName.InnerText = objmodule.PaneName
                        nodePane.AppendChild(xmlModule.CreateElement("modules"))
                        nodePanes.AppendChild(xmlTab.ImportNode(nodePane, True))
                    End If
                    nodeModules = nodePanes.SelectSingleNode("descendant::pane[name='" & objmodule.PaneName & "']/modules")

                    nodeModules.AppendChild(xmlTab.ImportNode(nodeModule, True))
                End If
            Next
            Return nodeTab
        End Function

#End Region

#Region "Obsolete"

        <Obsolete("This method is obsolete.  It has been replaced by GetTab(ByVal TabId As Integer, ByVal PortalId As Integer, ByVal ignoreCache As Boolean) ")> _
        Public Function GetTab(ByVal TabId As Integer) As TabInfo
            Dim dr As IDataReader = DataProvider.Instance().GetTab(TabId)
            Try
                Return FillTabInfo(dr)
            Finally
                If Not dr Is Nothing Then
                    dr.Close()
                End If
            End Try
        End Function

        <Obsolete("This method is obsolete.  It has been replaced by GetTabsByParentId(ByVal ParentId As Integer, ByVal PortalId As Integer) ")> _
        Public Function GetTabsByParentId(ByVal ParentId As Integer) As ArrayList
            Return FillTabInfoCollection(DataProvider.Instance().GetTabsByParentId(ParentId), False)
        End Function

        <Obsolete("This method is obsolete.  It has been replaced by UpdateTabOrder(ByVal objTab As TabInfo) ")> _
        Public Sub UpdateTabOrder(ByVal PortalID As Integer, ByVal TabId As Integer, ByVal TabOrder As Integer, ByVal Level As Integer, ByVal ParentId As Integer)
            Dim objTab As TabInfo = GetTab(TabId, PortalID, False)
            objTab.TabOrder = TabOrder
            objTab.Level = Level
            objTab.ParentId = ParentId
            UpdateTabOrder(objTab)
        End Sub


#End Region

    End Class


End Namespace

