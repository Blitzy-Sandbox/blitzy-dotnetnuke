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

Imports System.Collections.Specialized
Imports DotNetNuke.Services.Localization
Imports DotNetNuke.Framework


Namespace DotNetNuke.UI.WebControls

    ''' -----------------------------------------------------------------------------
    ''' Project:    DotNetNuke
    ''' Namespace:  DotNetNuke.UI.WebControls
    ''' Class:      DNNLocaleEditControl
    ''' -----------------------------------------------------------------------------
    ''' <summary>
    ''' The DNNLocaleEditControl control provides a standard UI component for selecting
    ''' a Locale
    ''' </summary>
    ''' <history>
    '''     [cnurse]	02/23/2006	created
    ''' </history>
    ''' -----------------------------------------------------------------------------
    <ToolboxData("<{0}:DNNLocaleEditControl runat=server></{0}:DNNLocaleEditControl>")> _
    Public Class DNNLocaleEditControl
        Inherits TextEditControl

#Region "Constructors"

        ''' -----------------------------------------------------------------------------
        ''' <summary>
        ''' Constructs a DNNLocaleEditControl
        ''' </summary>
        ''' <history>
        '''     [cnurse]	02/24/2006	created
        ''' </history>
        ''' -----------------------------------------------------------------------------
        Public Sub New()
        End Sub

#End Region

#Region "Protected Methods"

        ''' -----------------------------------------------------------------------------
        ''' <summary>
        ''' RenderViewMode renders the View (readonly) mode of the control
        ''' </summary>
        ''' <param name="writer">A HtmlTextWriter.</param>
        ''' <history>
        '''     [cnurse]	05/02/2006	created
        ''' </history>
        ''' -----------------------------------------------------------------------------
        Protected Overrides Sub RenderViewMode(ByVal writer As System.Web.UI.HtmlTextWriter)
            'For convenience create a DropDownList to use
            Dim cboLocale As New DropDownList

            'Load the List with Locales
            Localization.LoadCultureDropDownList(cboLocale, CultureDropDownTypes.NativeName, CType(Page, PageBase).PageCulture.Name)

            'Select the relevant item
            If Not cboLocale.Items.FindByValue(StringValue) Is Nothing Then
                cboLocale.ClearSelection()
                cboLocale.Items.FindByValue(StringValue).Selected = True
            End If

            ControlStyle.AddAttributesToRender(writer)
            writer.RenderBeginTag(HtmlTextWriterTag.Span)
            writer.Write(cboLocale.SelectedItem.Text)
            writer.RenderEndTag()
        End Sub

        ''' -----------------------------------------------------------------------------
        ''' <summary>
        ''' RenderEditMode renders the Edit mode of the control
        ''' </summary>
        ''' <param name="writer">A HtmlTextWriter.</param>
        ''' <history>
        '''     [cnurse]	02/27/2006	created
        ''' </history>
        ''' -----------------------------------------------------------------------------
        Protected Overrides Sub RenderEditMode(ByVal writer As System.Web.UI.HtmlTextWriter)
            'For convenience create a DropDownList to use
            Dim cboLocale As New DropDownList

            'Load the List with Locales
            Localization.LoadCultureDropDownList(cboLocale, CultureDropDownTypes.NativeName, CType(Page, PageBase).PageCulture.Name)

            'Select the relevant item
            If Not cboLocale.Items.FindByValue(StringValue) Is Nothing Then
                cboLocale.ClearSelection()
                cboLocale.Items.FindByValue(StringValue).Selected = True
            End If

            'Render the Select Tag
            ControlStyle.AddAttributesToRender(writer)
            writer.AddAttribute(HtmlTextWriterAttribute.Name, Me.UniqueID)
            writer.RenderBeginTag(HtmlTextWriterTag.Select)

            For I As Integer = 0 To cboLocale.Items.Count - 1
                Dim localeValue As String = cboLocale.Items(I).Value
                Dim localeName As String = cboLocale.Items(I).Text
                Dim isSelected As Boolean = cboLocale.Items(I).Selected

                'Add the Value Attribute
                writer.AddAttribute(HtmlTextWriterAttribute.Value, localeValue)

                If isSelected Then
                    'Add the Selected Attribute
                    writer.AddAttribute(HtmlTextWriterAttribute.Selected, "selected")
                End If

                'Render Option Tag
                writer.RenderBeginTag(HtmlTextWriterTag.Option)
                writer.Write(localeName)
                writer.RenderEndTag()
            Next

            'Close Select Tag
            writer.RenderEndTag()
        End Sub

#End Region

    End Class

End Namespace

