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
Imports DotNetNuke

Namespace DotNetNuke.Security.Permissions

#Region "PermissionController"
    Public Class PermissionController


        Public Function GetPermission(ByVal permissionID As Integer) As PermissionInfo
            Return CType(CBO.FillObject(DataProvider.Instance().GetPermission(permissionID), GetType(PermissionInfo)), PermissionInfo)
        End Function

        Public Function GetPermissionsByModuleDefID(ByVal ModuleDefID As Integer) As ArrayList
            Return CBO.FillCollection(DataProvider.Instance().GetPermissionsByModuleDefID(ModuleDefID), GetType(PermissionInfo))
        End Function

        Public Function GetPermissionsByModuleID(ByVal ModuleID As Integer) As ArrayList
            Return CBO.FillCollection(DataProvider.Instance().GetPermissionsByModuleID(ModuleID), GetType(PermissionInfo))
        End Function


        Public Function GetPermissionsByFolder(ByVal PortalID As Integer, ByVal Folder As String) As ArrayList
            Return CBO.FillCollection(DataProvider.Instance().GetPermissionsByFolderPath(PortalID, Folder), GetType(PermissionInfo))
        End Function

        Public Function GetPermissionByCodeAndKey(ByVal PermissionCode As String, ByVal PermissionKey As String) As ArrayList
            Return CBO.FillCollection(DataProvider.Instance().GetPermissionByCodeAndKey(PermissionCode, PermissionKey), GetType(PermissionInfo))
        End Function

        Public Function GetPermissionsByTabID(ByVal TabID As Integer) As ArrayList
            Return CBO.FillCollection(DataProvider.Instance().GetPermissionsByTabID(TabID), GetType(PermissionInfo))
        End Function

        Public Sub DeletePermission(ByVal permissionID As Integer)
            DataProvider.Instance().DeletePermission(permissionID)
        End Sub

        Public Function AddPermission(ByVal objPermission As PermissionInfo) As Integer
            Return CType(DataProvider.Instance().AddPermission(objPermission.PermissionCode, objPermission.ModuleDefID, objPermission.PermissionKey, objPermission.PermissionName), Integer)
        End Function

        Public Sub UpdatePermission(ByVal objPermission As PermissionInfo)
            DataProvider.Instance().UpdatePermission(objPermission.PermissionID, objPermission.PermissionCode, objPermission.ModuleDefID, objPermission.PermissionKey, objPermission.PermissionName)
        End Sub

    End Class

#End Region

End Namespace
