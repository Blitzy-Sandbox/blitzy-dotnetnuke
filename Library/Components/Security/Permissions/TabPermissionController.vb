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
Imports DotNetNuke
Imports DotNetNuke.Entities.Tabs

Namespace DotNetNuke.Security.Permissions


    Public Class TabPermissionController

#Region "Public Shared Methods"

        Public Shared Function HasTabPermission(ByVal PermissionKey As String) As Boolean
            Dim _PortalSettings As PortalSettings = PortalController.GetCurrentPortalSettings
            Return HasTabPermission(_PortalSettings.ActiveTab.TabPermissions, PermissionKey)
        End Function

        Public Shared Function HasTabPermission(ByVal objTabPermissions As Security.Permissions.TabPermissionCollection, ByVal PermissionKey As String) As Boolean
            Dim _PortalSettings As PortalSettings = PortalController.GetCurrentPortalSettings
            For Each objTabPermission As TabPermissionInfo In objTabPermissions
                If objTabPermission.PermissionKey = PermissionKey Then
                    If Null.IsNull(objTabPermission.UserID) Then
                        If PortalSecurity.IsInRoles(objTabPermission.RoleName) Then
                            Return True
                        End If
                    Else
                        If PortalSecurity.IsInRoles("[" & objTabPermission.UserID.ToString & "]") Then
                            Return True
                        End If
                    End If
                End If
            Next
            Return False
        End Function

#End Region

#Region "Private Methods"

        Private Sub ClearPermissionCache(ByVal tabId As Integer)
            Dim objTabs As New TabController
            Dim objTab As TabInfo = objTabs.GetTab(tabId, Null.NullInteger, False)
            DataCache.ClearTabPermissionsCache(objTab.PortalId)
        End Sub

        Private Function FillTabPermissionCollection(ByVal dr As IDataReader) As TabPermissionCollection
            Dim arr As New TabPermissionCollection()
            Try
                Dim obj As TabPermissionInfo
                While dr.Read
                    ' fill business object
                    obj = FillTabPermissionInfo(dr, False)
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

        Private Function FillTabPermissionDictionary(ByVal dr As IDataReader) As Dictionary(Of Integer, TabPermissionCollection)
            Dim dic As New Dictionary(Of Integer, TabPermissionCollection)
            Try
                Dim obj As TabPermissionInfo
                While dr.Read
                    ' fill business object
                    obj = FillTabPermissionInfo(dr, False)

                    ' add Tab Permission to dictionary
                    If dic.ContainsKey(obj.TabID) Then
                        'Add TabPermission to TabPermission Collection already in dictionary for TabId
                        dic(obj.TabID).Add(obj)
                    Else
                        'Create new TabPermission Collection for TabId
                        Dim collection As New TabPermissionCollection

                        'Add Permission to Collection
                        collection.Add(obj)

                        'Add Collection to Dictionary
                        dic.Add(obj.TabID, collection)
                    End If
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

        Private Function FillTabPermissionInfo(ByVal dr As IDataReader) As TabPermissionInfo
            Return FillTabPermissionInfo(dr, True)
        End Function

        Private Function FillTabPermissionInfo(ByVal dr As IDataReader, ByVal CheckForOpenDataReader As Boolean) As TabPermissionInfo
            Dim permissionInfo As TabPermissionInfo

            ' read datareader
            Dim canContinue As Boolean = True
            If CheckForOpenDataReader Then
                canContinue = False
                If dr.Read Then
                    canContinue = True
                End If
            End If

            If canContinue Then
                permissionInfo = New TabPermissionInfo()
                permissionInfo.TabPermissionID = Convert.ToInt32(Null.SetNull(dr("TabPermissionID"), permissionInfo.TabPermissionID))
                permissionInfo.TabID = Convert.ToInt32(Null.SetNull(dr("TabID"), permissionInfo.TabID))
                permissionInfo.PermissionID = Convert.ToInt32(Null.SetNull(dr("PermissionID"), permissionInfo.PermissionID))
                permissionInfo.UserID = Convert.ToInt32(Null.SetNull(dr("UserID"), permissionInfo.UserID))
                permissionInfo.Username = Convert.ToString(Null.SetNull(dr("Username"), permissionInfo.Username))
                permissionInfo.DisplayName = Convert.ToString(Null.SetNull(dr("DisplayName"), permissionInfo.DisplayName))
                If permissionInfo.UserID = Null.NullInteger Then
                    permissionInfo.RoleID = Convert.ToInt32(Null.SetNull(dr("RoleID"), permissionInfo.RoleID))
                    permissionInfo.RoleName = Convert.ToString(Null.SetNull(dr("RoleName"), permissionInfo.RoleName))
                Else
                    permissionInfo.RoleID = Integer.Parse(glbRoleNothing)
                    permissionInfo.RoleName = ""
                End If
                permissionInfo.AllowAccess = Convert.ToBoolean(Null.SetNull(dr("AllowAccess"), permissionInfo.AllowAccess))
                permissionInfo.PermissionCode = Convert.ToString(Null.SetNull(dr("PermissionCode"), permissionInfo.PermissionCode))
                permissionInfo.PermissionKey = Convert.ToString(Null.SetNull(dr("PermissionKey"), permissionInfo.PermissionKey))
                permissionInfo.PermissionName = Convert.ToString(Null.SetNull(dr("PermissionName"), permissionInfo.PermissionName))
            Else
                permissionInfo = Nothing
            End If

            Return permissionInfo

        End Function

        Private Function GetTabPermissionsDictionary(ByVal PortalId As Integer) As Dictionary(Of Integer, TabPermissionCollection)

            'Get the Cache Key
            Dim key As String = String.Format(DataCache.TabPermissionCacheKey, PortalId)

            'Try fetching the Dictionary from the Cache
            Dim dicTabPermissions As Dictionary(Of Integer, TabPermissionCollection) = CType(DataCache.GetPersistentCacheItem(key, GetType(Dictionary(Of Integer, TabPermissionCollection))), Dictionary(Of Integer, TabPermissionCollection))

            If dicTabPermissions Is Nothing Then
                'tabPermission caching settings
                Dim timeOut As Int32 = DataCache.TabPermissionCacheTimeOut * Convert.ToInt32(Common.Globals.PerformanceSetting)

                'Get the Dictionary from the database
                dicTabPermissions = FillTabPermissionDictionary(DataProvider.Instance().GetTabPermissionsByPortal(PortalId))

                'Cache the Dictionary
                If timeOut > 0 Then
                    DataCache.SetCache(key, dicTabPermissions, TimeSpan.FromMinutes(timeOut), True)
                End If
            End If

            'Return the Dictionary
            Return dicTabPermissions

        End Function

#End Region

#Region "Public Methods"

        Public Function AddTabPermission(ByVal objTabPermission As TabPermissionInfo) As Integer
            Dim Id As Integer = CType(DataProvider.Instance().AddTabPermission(objTabPermission.TabID, objTabPermission.PermissionID, objTabPermission.RoleID, objTabPermission.AllowAccess, objTabPermission.UserID), Integer)
            ClearPermissionCache(objTabPermission.TabID)
            Return Id
        End Function

        Public Sub DeleteTabPermission(ByVal TabPermissionID As Integer)
            DataProvider.Instance().DeleteTabPermission(TabPermissionID)
        End Sub

        Public Sub DeleteTabPermissionsByTabID(ByVal TabID As Integer)
            DataProvider.Instance().DeleteTabPermissionsByTabID(TabID)
            ClearPermissionCache(TabID)
        End Sub

        Public Sub DeleteTabPermissionsByUserID(ByVal objUser As UserInfo)
            DataProvider.Instance().DeleteTabPermissionsByUserID(objUser.PortalID, objUser.UserID)
            DataCache.ClearTabPermissionsCache(objUser.PortalID)
        End Sub

        Public Function GetTabPermissions(ByVal tabPermissions As TabPermissionCollection, ByVal PermissionKey As String) As String
            Dim strRoles As String = ""
            Dim strUsers As String = ""
            For Each objTabPermission As TabPermissionInfo In tabPermissions
                If objTabPermission.AllowAccess = True AndAlso objTabPermission.PermissionKey = PermissionKey Then
                    If Null.IsNull(objTabPermission.UserID) Then
                        strRoles += objTabPermission.RoleName + ";"
                    Else
                        strUsers += "[" + objTabPermission.UserID.ToString + "];"
                    End If
                End If
            Next
            Return ";" & strRoles & strUsers
        End Function

        Public Function GetTabPermissionsCollectionByTabID(ByVal TabID As Integer, ByVal PortalId As Integer) As TabPermissionCollection
            Dim bFound As Boolean = False

            'Get the Portal TabPermission Dictionary
            Dim dicTabPermissions As Dictionary(Of Integer, TabPermissionCollection) = GetTabPermissionsDictionary(PortalId)

            'Get the Collection from the Dictionary
            Dim tabPermissions As TabPermissionCollection = Nothing
            bFound = dicTabPermissions.TryGetValue(TabID, tabPermissions)

            If Not bFound Then
                'try the database
                tabPermissions = FillTabPermissionCollection(DataProvider.Instance().GetTabPermissionsByTabID(TabID, -1))
            End If

            Return tabPermissions
        End Function

        Public Sub UpdateTabPermission(ByVal objTabPermission As TabPermissionInfo)
            DataProvider.Instance().UpdateTabPermission(objTabPermission.TabPermissionID, objTabPermission.TabID, objTabPermission.PermissionID, objTabPermission.RoleID, objTabPermission.AllowAccess, objTabPermission.UserID)
            ClearPermissionCache(objTabPermission.TabID)
        End Sub

#End Region

#Region "Obsolete Methods"

        Private Function FillTabPermissionInfoList(ByVal dr As IDataReader) As ArrayList
            Dim arr As New ArrayList
            Try
                Dim obj As TabPermissionInfo
                While dr.Read
                    ' fill business object
                    obj = FillTabPermissionInfo(dr, False)
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

        <Obsolete("This method has been deprecated.  This should have been declared as Friend as it was never meant to be used outside of the core.")> _
        Public Function GetTabPermissionsByPortal(ByVal PortalID As Integer) As ArrayList
            Dim key As String = String.Format(DataCache.TabPermissionCacheKey, PortalID)

            Dim arrTabPermissions As ArrayList = CType(DataCache.GetCache(key), ArrayList)

            If arrTabPermissions Is Nothing Then
                'tabPermission caching settings
                Dim timeOut As Int32 = DataCache.TabPermissionCacheTimeOut * Convert.ToInt32(Common.Globals.PerformanceSetting)

                arrTabPermissions = FillTabPermissionInfoList(DataProvider.Instance().GetTabPermissionsByPortal(PortalID))

                'Cache tabs
                If timeOut > 0 Then
                    DataCache.SetCache(key, arrTabPermissions, TimeSpan.FromMinutes(timeOut))
                End If
            End If
            Return arrTabPermissions
        End Function

        <Obsolete("This method has been deprecated.  Please use GetTabPermissionsCollectionByTabID(TabId, PortalId)")> _
        Public Function GetTabPermissionsByTabID(ByVal TabID As Integer) As ArrayList
            Return FillTabPermissionInfoList(DataProvider.Instance().GetTabPermissionsByTabID(TabID, -1))
        End Function

        <Obsolete("This method has been deprecated.  GetTabPermissions(TabPermissionCollection, String) ")> _
        Public Function GetTabPermissionsByTabID(ByVal arrTabPermissions As ArrayList, ByVal TabID As Integer, ByVal PermissionKey As String) As String
            Dim strRoles As String = ";"
            Dim strUsers As String = ";"
            Dim i As Integer
            For i = 0 To arrTabPermissions.Count - 1
                Dim objTabPermission As Security.Permissions.TabPermissionInfo = CType(arrTabPermissions(i), Security.Permissions.TabPermissionInfo)
                If objTabPermission.TabID = TabID AndAlso objTabPermission.AllowAccess = True AndAlso objTabPermission.PermissionKey = PermissionKey Then
                    If Null.IsNull(objTabPermission.UserID) Then
                        strRoles += objTabPermission.RoleName + ";"
                    Else
                        strUsers += "[" + objTabPermission.UserID.ToString + "];"
                    End If
                End If
            Next
            Return strRoles + strUsers
        End Function

        <Obsolete("This method has been deprecated.  Please use GetTabPermissionsCollectionByTabID(TabId, PortalId)")> _
        Public Function GetTabPermissionsByTabID(ByVal arrTabPermissions As ArrayList, ByVal TabID As Integer) As TabPermissionCollection
            Dim p As New Security.Permissions.TabPermissionCollection
            Dim i As Integer
            For i = 0 To arrTabPermissions.Count - 1
                Dim objTabPermission As TabPermissionInfo = CType(arrTabPermissions(i), TabPermissionInfo)
                If objTabPermission.TabID = TabID Then
                    p.Add(objTabPermission)
                End If
            Next
            Return p
        End Function

        <Obsolete("This method has been deprecated.  Please use GetTabPermissionsCollectionByTabID(TabId, PortalId)")> _
        Public Function GetTabPermissionsCollectionByTabID(ByVal TabID As Integer) As Security.Permissions.TabPermissionCollection
            Return FillTabPermissionCollection(DataProvider.Instance().GetTabPermissionsByTabID(TabID, -1))
        End Function

        <Obsolete("This method has been deprecated.  Please use GetTabPermissionsCollectionByTabID(TabId, PortalId)")> _
        Public Function GetTabPermissionsCollectionByTabID(ByVal arrTabPermissions As ArrayList, ByVal TabID As Integer) As Security.Permissions.TabPermissionCollection
            Dim objTabPermissionCollection As New Security.Permissions.TabPermissionCollection(arrTabPermissions, TabID)
            Return objTabPermissionCollection
        End Function

#End Region

    End Class


End Namespace
