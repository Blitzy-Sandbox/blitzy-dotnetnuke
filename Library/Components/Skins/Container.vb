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
Imports System.Web.UI

Imports DotNetNuke.Entities.Modules
Imports DotNetNuke.Entities.Modules.Actions

'Legacy Support
Namespace DotNetNuke

    <Obsolete("This class is obsolete.  Please use DotNetNuke.UI.Containers.Container.")> _
    Public Class Container
        Inherits DotNetNuke.UI.Containers.Container
    End Class

End Namespace

Namespace DotNetNuke.UI.Containers

    ''' -----------------------------------------------------------------------------
    ''' Project	 : DotNetNuke
    ''' Namespace: DotNetNuke.UI.Containers
    ''' Class	 : Container
    ''' -----------------------------------------------------------------------------
    ''' <summary>
    ''' Container is the base for the Containers 
    ''' </summary>
    ''' <remarks>
    ''' </remarks>
    ''' <history>
    ''' 	[cnurse]	07/04/2005	Documented
    ''' </history>
    ''' -----------------------------------------------------------------------------
    Public Class Container

        Inherits System.Web.UI.UserControl

#Region "Public Members"

        Public ReadOnly Property SkinPath() As String
            Get
                Return Me.TemplateSourceDirectory & "/"
            End Get
        End Property

#End Region

#Region "Public Methods"

        ''' -----------------------------------------------------------------------------
        ''' <summary>
        ''' GetPortalModuleBase gets the parent PortalModuleBase Control
        ''' </summary>
        ''' <remarks>
        ''' </remarks>
        ''' <history>
        ''' 	[cnurse]	07/04/2005	Documented
        ''' </history>
        ''' -----------------------------------------------------------------------------
        Public Shared Function GetPortalModuleBase(ByVal objControl As UserControl) As Entities.Modules.PortalModuleBase

            Dim objPortalModuleBase As Entities.Modules.PortalModuleBase = Nothing

            Dim ctlPanel As Panel

            If TypeOf objControl Is UI.Skins.SkinObjectBase Then
                ctlPanel = CType(objControl.Parent.FindControl("ModuleContent"), Panel)
            Else
                ctlPanel = CType(objControl.FindControl("ModuleContent"), Panel)
            End If

            If Not ctlPanel Is Nothing Then
                Try
                    objPortalModuleBase = CType(ctlPanel.Controls(1), PortalModuleBase)
                Catch
                    ' check if it is nested within an UpdatePanel 
                    Try
                        objPortalModuleBase = CType(ctlPanel.Controls(0).Controls(0).Controls(1), PortalModuleBase)
                    Catch
                    End Try
                End Try
            End If

            If objPortalModuleBase Is Nothing Then
                objPortalModuleBase = New PortalModuleBase
                objPortalModuleBase.ModuleConfiguration = New ModuleInfo
            End If

            Return objPortalModuleBase

        End Function

#End Region

    End Class

End Namespace