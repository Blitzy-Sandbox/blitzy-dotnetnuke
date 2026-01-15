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

Imports System.Collections.Generic
Imports System.IO
Imports System.Xml.XPath

Namespace DotNetNuke.Services.Packages

    ''' -----------------------------------------------------------------------------
    ''' <summary>
    ''' The PackageModuleBase class provides a base class for Package controls
    ''' </summary>
    ''' <remarks>
    ''' </remarks>
    ''' <history>
    ''' 	[cnurse]	08/15/2007	Created
    ''' </history>
    ''' -----------------------------------------------------------------------------
    Public Class PackageModuleBase
        Inherits DotNetNuke.Entities.Modules.PortalModuleBase

#Region "Protected Properties"

        ''' -----------------------------------------------------------------------------
        ''' <summary>
        ''' Gets the Package ID
        ''' </summary>
        ''' <history>
        ''' 	[cnurse]	08/15/2007	Created
        ''' </history>
        ''' -----------------------------------------------------------------------------
        Protected ReadOnly Property PackageID() As Integer
            Get
                Dim _PackageID As Integer = Null.NullInteger
                If Not String.IsNullOrEmpty(Request.QueryString("packageId")) Then
                    _PackageID = Integer.Parse(Request.QueryString("packageId"))
                End If
                Return _PackageID
            End Get
        End Property

        ''' -----------------------------------------------------------------------------
        ''' <summary>
        ''' Gets the Package
        ''' </summary>
        ''' <history>
        ''' 	[cnurse]	08/15/2007	Created
        ''' </history>
        ''' -----------------------------------------------------------------------------
        Protected ReadOnly Property Package() As PackageInfo
            Get
                Dim _Package As PackageInfo = Nothing
                If PackageID > 0 Then
                    _Package = PackageController.GetPackage(PackageID)
                End If
                Return _Package
            End Get
        End Property

#End Region

    End Class

End Namespace
