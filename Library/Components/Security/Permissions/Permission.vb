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
Imports System.Xml.Serialization

Namespace DotNetNuke.Security.Permissions

#Region "PermissionInfo"
    Public Class PermissionInfo

        ' local property declarations
        Dim _permissionID As Integer
        Dim _permissionCode As String
        Dim _moduleDefID As Integer
        Dim _permissionKey As String
        Dim _PermissionName As String

        ' initialization
        Public Sub New()
        End Sub 'New

#Region "Public Properties"
        <XmlElement("permissionid")> Public Property PermissionID() As Integer
            Get
                Return _permissionID
            End Get
            Set(ByVal Value As Integer)
                _permissionID = Value
            End Set
        End Property

        <XmlElement("permissioncode")> Public Property PermissionCode() As String
            Get
                Return _permissionCode
            End Get
            Set(ByVal Value As String)
                _permissionCode = Value
            End Set
        End Property

        <XmlIgnore()> Public Property ModuleDefID() As Integer
            Get
                Return _moduleDefID
            End Get
            Set(ByVal Value As Integer)
                _moduleDefID = Value
            End Set
        End Property

        <XmlElement("permissionkey")> Public Property PermissionKey() As String
            Get
                Return _permissionKey
            End Get
            Set(ByVal Value As String)
                _permissionKey = Value
            End Set
        End Property

        <XmlIgnore()> Public Property PermissionName() As String
            Get
                Return _PermissionName
            End Get
            Set(ByVal Value As String)
                _PermissionName = Value
            End Set
        End Property
#End Region

	End Class
#End Region

End Namespace
