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
Imports System.Collections
Imports System.Configuration
Imports System.Data
Imports System.Xml.Serialization

Namespace DotNetNuke.Security.Roles

    ''' -----------------------------------------------------------------------------
    ''' Project:    DotNetNuke
    ''' Namespace:  DotNetNuke.Security.Roles
    ''' Class:      RoleGroupInfo
    ''' -----------------------------------------------------------------------------
    ''' <summary>
    ''' The RoleGroupInfo class provides the Entity Layer RoleGroup object
    ''' </summary>
    ''' <history>
    '''     [cnurse]    01/03/2006  made compatible with .NET 2.0
    ''' </history>
    ''' -----------------------------------------------------------------------------
    Public Class RoleGroupInfo
        Private _RoleGroupID As Integer
        Private _PortalID As Integer
        Private _RoleGroupName As String
        Private _Description As String

        ''' -----------------------------------------------------------------------------
        ''' <summary>
        ''' Gets and sets the RoleGroup Id
        ''' </summary>
        ''' <value>An Integer representing the Id of the RoleGroup</value>
        ''' -----------------------------------------------------------------------------
        Public Property RoleGroupID() As Integer
            Get
                Return _RoleGroupID
            End Get
            Set(ByVal Value As Integer)
                _RoleGroupID = Value
            End Set
        End Property

        ''' -----------------------------------------------------------------------------
        ''' <summary>
        ''' Gets and sets the Portal Id for the RoleGroup
        ''' </summary>
        ''' <value>An Integer representing the Id of the Portal</value>
        ''' -----------------------------------------------------------------------------
        Public Property PortalID() As Integer
            Get
                Return _PortalID
            End Get
            Set(ByVal Value As Integer)
                _PortalID = Value
            End Set
        End Property

        ''' -----------------------------------------------------------------------------
        ''' <summary>
        ''' Gets and sets the RoleGroup Name
        ''' </summary>
        ''' <value>A string representing the Name of the RoleGroup</value>
        ''' -----------------------------------------------------------------------------
        Public Property RoleGroupName() As String
            Get
                Return _RoleGroupName
            End Get
            Set(ByVal Value As String)
                _RoleGroupName = Value
            End Set
        End Property

        ''' -----------------------------------------------------------------------------
        ''' <summary>
        ''' Gets an sets the Description of the RoleGroup
        ''' </summary>
        ''' <value>A string representing the description of the RoleGroup</value>
        ''' -----------------------------------------------------------------------------
        Public Property Description() As String
            Get
                Return _Description
            End Get
            Set(ByVal Value As String)
                _Description = Value
            End Set
        End Property

    End Class

End Namespace
