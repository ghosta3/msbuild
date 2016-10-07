﻿// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//-----------------------------------------------------------------------
// </copyright>
// <summary>A container for project elements.</summary>
//-----------------------------------------------------------------------

using System;
using System.Text;
using System.Collections.Generic;
using System.Linq;
using System.Xml;
using System.Diagnostics;
using Microsoft.Build.Framework;
using Microsoft.Build.Shared;
using System.Collections.ObjectModel;
using Microsoft.Build.Construction;
using Microsoft.Build.Collections;
using Microsoft.Build.Internal;

namespace Microsoft.Build.Construction
{
    /// <summary>
    /// A container for project elements
    /// </summary>
    public abstract class ProjectElementContainer : ProjectElement
    {
        const string DEFAULT_INDENT = "  ";

        /// <summary>
        /// Number of children of any kind
        /// </summary>
        private int _count;

        /// <summary>
        /// Constructor called by ProjectRootElement only.
        /// XmlElement is set directly after construction.
        /// </summary>
        /// <comment>
        /// Should ideally be protected+internal.
        /// </comment> 
        internal ProjectElementContainer()
            : base()
        {
        }

        /// <summary>
        /// Constructor called by derived classes, except from ProjectRootElement.
        /// Parameters may not be null, except parent.
        /// </summary>
        /// <comment>
        /// Should ideally be protected+internal.
        /// </comment>
        internal ProjectElementContainer(XmlElement xmlElement, ProjectElementContainer parent, ProjectRootElement containingProject)
            : base(xmlElement, parent, containingProject)
        {
        }

        /// <summary>
        /// Get an enumerator over all children, gotten recursively.
        /// Walks the children in a depth-first manner.
        /// </summary>
        public IEnumerable<ProjectElement> AllChildren
        {
            get { return GetChildrenRecursively(); }
        }

        /// <summary>
        /// Get enumerable over all the children
        /// </summary>
        public ICollection<ProjectElement> Children
        {
            [DebuggerStepThrough]
            get
            {
                return new Microsoft.Build.Collections.ReadOnlyCollection<ProjectElement>
                    (
                        new ProjectElementSiblingEnumerable(FirstChild)
                    );
            }
        }

        /// <summary>
        /// Get enumerable over all the children, starting from the last
        /// </summary>
        public ICollection<ProjectElement> ChildrenReversed
        {
            [DebuggerStepThrough]
            get
            {
                return new Microsoft.Build.Collections.ReadOnlyCollection<ProjectElement>
                    (
                        new ProjectElementSiblingEnumerable(LastChild, false /* reverse */)
                    );
            }
        }

        /// <summary>
        /// Number of children of any kind
        /// </summary>
        public int Count
        {
            [DebuggerStepThrough]
            get
            { return _count; }
        }

        /// <summary>
        /// First child, if any, otherwise null.
        /// Cannot be set directly; use <see cref="PrependChild">PrependChild()</see>.
        /// </summary>
        public ProjectElement FirstChild
        {
            [DebuggerStepThrough]
            get;
            [DebuggerStepThrough]
            private set;
        }

        /// <summary>
        /// Last child, if any, otherwise null.
        /// Cannot be set directly; use <see cref="AppendChild">AppendChild()</see>.
        /// </summary>
        public ProjectElement LastChild
        {
            [DebuggerStepThrough]
            get;
            [DebuggerStepThrough]
            private set;
        }

        /// <summary>
        /// Insert the child after the reference child.
        /// Reference child if provided must be parented by this element.
        /// Reference child may be null, in which case this is equivalent to <see cref="PrependChild">PrependChild(child)</see>.
        /// Throws if the parent is not itself parented.
        /// Throws if the reference node does not have this node as its parent.
        /// Throws if the node to add is already parented.
        /// Throws if the node to add was created from a different project than this node.
        /// </summary>
        /// <remarks>
        /// Semantics are those of XmlNode.InsertAfterChild.
        /// </remarks>
        public void InsertAfterChild(ProjectElement child, ProjectElement reference)
        {
            ErrorUtilities.VerifyThrowArgumentNull(child, "child");

            if (reference == null)
            {
                PrependChild(child);
                return;
            }

            VerifyForInsertBeforeAfterFirst(child, reference);

            child.VerifyThrowInvalidOperationAcceptableLocation(this, reference, reference.NextSibling);

            child.Parent = this;

            if (LastChild == reference)
            {
                LastChild = child;
            }

            child.PreviousSibling = reference;
            child.NextSibling = reference.NextSibling;

            reference.NextSibling = child;

            if (child.NextSibling != null)
            {
                ErrorUtilities.VerifyThrow(child.NextSibling.PreviousSibling == reference, "Invalid structure");
                child.NextSibling.PreviousSibling = child;
            }

            AddToXml(child);

            _count++;
            MarkDirty("Insert element {0}", child.ElementName);
        }

        /// <summary>
        /// Insert the child before the reference child.
        /// Reference child if provided must be parented by this element.
        /// Reference child may be null, in which case this is equivalent to <see cref="AppendChild">AppendChild(child)</see>.
        /// Throws if the parent is not itself parented.
        /// Throws if the reference node does not have this node as its parent.
        /// Throws if the node to add is already parented.
        /// Throws if the node to add was created from a different project than this node.
        /// </summary>
        /// <remarks>
        /// Semantics are those of XmlNode.InsertBeforeChild.
        /// </remarks>
        public void InsertBeforeChild(ProjectElement child, ProjectElement reference)
        {
            ErrorUtilities.VerifyThrowArgumentNull(child, "child");

            if (reference == null)
            {
                AppendChild(child);
                return;
            }

            VerifyForInsertBeforeAfterFirst(child, reference);

            child.VerifyThrowInvalidOperationAcceptableLocation(this, reference.PreviousSibling, reference);

            child.Parent = this;

            if (FirstChild == reference)
            {
                FirstChild = child;
            }

            child.PreviousSibling = reference.PreviousSibling;
            child.NextSibling = reference;

            reference.PreviousSibling = child;

            if (child.PreviousSibling != null)
            {
                ErrorUtilities.VerifyThrow(child.PreviousSibling.NextSibling == reference, "Invalid structure");
                child.PreviousSibling.NextSibling = child;
            }

            AddToXml(child);

            _count++;
            MarkDirty("Insert element {0}", child.ElementName);
        }

        /// <summary>
        /// Appends the provided element as the last child.
        /// Throws if the parent is not itself parented.
        /// Throws if the node to add is already parented.
        /// Throws if the node to add was created from a different project than this node.
        /// </summary>
        public void AppendChild(ProjectElement child)
        {
            if (LastChild == null)
            {
                AddInitialChild(child);
            }
            else
            {
                ErrorUtilities.VerifyThrow(FirstChild != null, "Invalid structure");
                InsertAfterChild(child, LastChild);
            }
        }

        /// <summary>
        /// Appends the provided element as the last child.
        /// Throws if the parent is not itself parented.
        /// Throws if the node to add is already parented.
        /// Throws if the node to add was created from a different project than this node.
        /// </summary>
        public void PrependChild(ProjectElement child)
        {
            if (FirstChild == null)
            {
                AddInitialChild(child);
            }
            else
            {
                ErrorUtilities.VerifyThrow(LastChild != null, "Invalid structure");
                InsertBeforeChild(child, FirstChild);
                return;
            }
        }

        /// <summary>
        /// Removes the specified child.
        /// Throws if the child is not currently parented by this object.
        /// This is O(1).
        /// May be safely called during enumeration of the children.
        /// </summary>
        /// <remarks>
        /// This is actually safe to call during enumeration of children, because it
        /// doesn't bother to clear the child's NextSibling (or PreviousSibling) pointers.
        /// To determine whether a child is unattached, check whether its parent is null,
        /// or whether its NextSibling and PreviousSibling point back at it.
        /// DO NOT BREAK THIS VERY USEFUL SAFETY CONTRACT.
        /// </remarks>
        public void RemoveChild(ProjectElement child)
        {
            ErrorUtilities.VerifyThrowArgumentNull(child, "child");

            ErrorUtilities.VerifyThrowArgument(child.Parent == this, "OM_NodeNotAlreadyParentedByThis");

            child.ClearParent();

            if (child.PreviousSibling != null)
            {
                child.PreviousSibling.NextSibling = child.NextSibling;
            }

            if (child.NextSibling != null)
            {
                child.NextSibling.PreviousSibling = child.PreviousSibling;
            }

            if (Object.ReferenceEquals(child, FirstChild))
            {
                FirstChild = child.NextSibling;
            }

            if (Object.ReferenceEquals(child, LastChild))
            {
                LastChild = child.PreviousSibling;
            }

            RemoveFromXml(child);

            _count--;
            MarkDirty("Remove element {0}", child.ElementName);
        }

        /// <summary>
        /// Remove all the children, if any.
        /// </summary>
        /// <remarks>
        /// It is safe to modify the children in this way
        /// during enumeration. See <cref see="RemoveChild">RemoveChild</cref>.
        /// </remarks>
        public void RemoveAllChildren()
        {
            foreach (ProjectElement child in Children)
            {
                RemoveChild(child);
            }
        }

        /// <summary>
        /// Applies properties from the specified type to this instance.
        /// </summary>
        /// <param name="element">The element to act as a template to copy from.</param>
        public virtual void DeepCopyFrom(ProjectElementContainer element)
        {
            ErrorUtilities.VerifyThrowArgumentNull(element, "element");
            ErrorUtilities.VerifyThrowArgument(this.GetType().IsEquivalentTo(element.GetType()), "element");

            if (this == element)
            {
                return;
            }

            this.RemoveAllChildren();
            this.CopyFrom(element);

            foreach (var child in element.Children)
            {
                var childContainer = child as ProjectElementContainer;
                if (childContainer != null)
                {
                    childContainer.DeepClone(this.ContainingProject, this);
                }
                else
                {
                    this.AppendChild(child.Clone(this.ContainingProject));
                }
            }
        }

        /// <summary>
        /// Appends the provided child.
        /// Does not dirty the project, does not add an element, does not set the child's parent,
        /// and does not check the parent's future siblings and parent are acceptable.
        /// Called during project load, when the child can be expected to 
        /// already have a parent and its element is already connected to the
        /// parent's element.
        /// All that remains is to set FirstChild/LastChild and fix up the linked list.
        /// </summary>
        internal void AppendParentedChildNoChecks(ProjectElement child)
        {
            ErrorUtilities.VerifyThrow(child.Parent == this, "Expected parent already set");
            ErrorUtilities.VerifyThrow(child.PreviousSibling == null && child.NextSibling == null, "Invalid structure");

            if (LastChild == null)
            {
                FirstChild = child;
            }
            else
            {
                child.PreviousSibling = LastChild;
                LastChild.NextSibling = child;
            }

            LastChild = child;

            _count++;
        }

        /// <summary>
        /// Returns a clone of this project element and all its children.
        /// </summary>
        /// <param name="factory">The factory to use for creating the new instance.</param>
        /// <param name="parent">The parent to append the cloned element to as a child.</param>
        /// <returns>The cloned element.</returns>
        protected internal virtual ProjectElementContainer DeepClone(ProjectRootElement factory, ProjectElementContainer parent)
        {
            var clone = (ProjectElementContainer)this.Clone(factory);
            if (parent != null)
            {
                parent.AppendChild(clone);
            }

            foreach (var child in this.Children)
            {
                var childContainer = child as ProjectElementContainer;
                if (childContainer != null)
                {
                    childContainer.DeepClone(clone.ContainingProject, clone);
                }
                else
                {
                    clone.AppendChild(child.Clone(clone.ContainingProject));
                }
            }

            return clone;
        }

        private void SetElementAsAttributeValue(ProjectElement child)
        {
            //  Assumes that child.ExpressedAsAttribute is true
            Debug.Assert(child.ExpressedAsAttribute, nameof(SetElementAsAttributeValue) + " method requires that " +
                nameof(child.ExpressedAsAttribute) + " property of child is true");

            string value = Microsoft.Build.Internal.Utilities.GetXmlNodeInnerContents(child.XmlElement);
            ProjectXmlUtilities.SetOrRemoveAttribute(XmlElement, child.XmlElement.Name, value);
        }

        /// <summary>
        /// If child "element" is actually represented as an attribute, update the value in the corresponding Xml attribute
        /// </summary>
        /// <param name="child">A child element which might be represented as an attribute</param>
        internal void UpdateElementValue(ProjectElement child)
        {
            if (child.ExpressedAsAttribute)
            {
                SetElementAsAttributeValue(child);
            }
        }

        /// <summary>
        /// Adds a ProjectElement to the Xml tree
        /// </summary>
        /// <param name="child">A child to add to the Xml tree, which has already been added to the ProjectElement tree</param>
        /// <remarks>
        /// The MSBuild construction APIs keep a tree of ProjectElements and a parallel Xml tree which consists of
        /// objects from System.Xml.  This is a helper method which adds an XmlElement or Xml attribute to the Xml
        /// tree after the corresponding ProjectElement has been added to the construction API tree, and fixes up
        /// whitespace as necessary.
        /// </remarks>
        internal void AddToXml(ProjectElement child)
        {
            if (child.ExpressedAsAttribute)
            {
                //  Assume that the name of the child has already been validated to conform with rules in XmlUtilities.VerifyThrowArgumentValidElementName

                //  Make sure we're not trying to add multiple attributes with the same name
                ProjectErrorUtilities.VerifyThrowInvalidProject(!XmlElement.HasAttribute(child.XmlElement.Name),
                    XmlElement.Location, "InvalidChildElementDueToDuplication", child.XmlElement.Name, ElementName);

                SetElementAsAttributeValue(child);
            }
            else
            {
                //  We want to add the XmlElement to the same position in the child list as the corresponding ProjectElement.
                //  Depending on whether the child ProjectElement has a PreviousSibling or a NextSibling, we may need to
                //  use the InsertAfter, InsertBefore, or AppendChild methods to add it in the right place.
                //
                //  Also, if PreserveWhitespace is true, then the elements we add won't automatically get indented, so
                //  we try to match the surrounding formatting.
                if (child.PreviousSibling != null)
                {
                    //  Add after previous sibling
                    XmlElement.InsertAfter(child.XmlElement, child.PreviousSibling.XmlElement);
                    if (XmlDocument.PreserveWhitespace)
                    {
                        //  Try to match the surrounding formatting by checking the whitespace that precedes the node we inserted
                        //  after, and inserting the same whitespace between the previous node and the one we added
                        if (child.PreviousSibling.XmlElement.PreviousSibling != null &&
                            child.PreviousSibling.XmlElement.PreviousSibling.NodeType == XmlNodeType.Whitespace)
                        {
                            var newWhitespaceNode = XmlDocument.CreateWhitespace(child.PreviousSibling.XmlElement.PreviousSibling.Value);
                            XmlElement.InsertAfter(newWhitespaceNode, child.PreviousSibling.XmlElement);
                        }
                    }
                }
                else if (child.NextSibling != null)
                {
                    //  Add as first child
                    XmlElement.InsertBefore(child.XmlElement, child.NextSibling.XmlElement);

                    if (XmlDocument.PreserveWhitespace)
                    {
                        //  Try to match the surrounding formatting by by checking the whitespace that precedes where we inserted
                        //  the new node, and inserting the same whitespace between the node we added and the one after it.
                        if (child.XmlElement.PreviousSibling != null &&
                            child.XmlElement.PreviousSibling.NodeType == XmlNodeType.Whitespace)
                        {
                            var newWhitespaceNode = XmlDocument.CreateWhitespace(child.XmlElement.PreviousSibling.Value);
                            XmlElement.InsertBefore(newWhitespaceNode, child.NextSibling.XmlElement);
                        }
                    }
                }
                else
                {
                    //  Add as only child
                    XmlElement.AppendChild(child.XmlElement);

                    if (XmlDocument.PreserveWhitespace)
                    {
                        //  Try to match the surrounding formatting and add one indentation level
                        if (XmlElement.FirstChild.NodeType == XmlNodeType.Whitespace)
                        {
                            //  This container had a whitespace node, which should generally be a newline and the indent
                            //  before the closing tag.  So we add the default indentation to it so the child will now be indented
                            //  further, and then create a new whitespace node after the child so the closing tag will be on
                            //  a new line with the same indentation.
                            //  If the whitespace we end up copying isn't actually (newline + indentation) like we expect, then it
                            //  should still be OK to copy it, as we'll still be trying to match the surrounding formatting.
                            string whitespace = XmlElement.FirstChild.Value;
                            XmlElement.FirstChild.Value = whitespace + DEFAULT_INDENT;
                            var newWhitespaceNode = XmlDocument.CreateWhitespace(whitespace);
                            XmlElement.InsertAfter(newWhitespaceNode, child.XmlElement);
                        }
                        else if (XmlElement.PreviousSibling != null &&
                                 XmlElement.PreviousSibling.NodeType == XmlNodeType.Whitespace)
                        {
                            //  This container didn't have any whitespace in it.  This probably means it didn't have separate open
                            //  and close tags.  So add a whitespace node before the new child with additional indentation over the
                            //  container's indentation, and add a whitespace node with the same level of indentation as the container
                            //  after the new child so the closing tag will be indented properly.
                            string parentWhitespace = XmlElement.PreviousSibling.Value;
                            var indentedWhitespaceNode = XmlDocument.CreateWhitespace(parentWhitespace + DEFAULT_INDENT);
                            XmlElement.InsertBefore(indentedWhitespaceNode, child.XmlElement);
                            var unindentedWhitespaceNode = XmlDocument.CreateWhitespace(parentWhitespace);
                            XmlElement.InsertAfter(unindentedWhitespaceNode, child.XmlElement);
                        }
                    }
                }
            }
        }

        internal void RemoveFromXml(ProjectElement child)
        {
            if (child.ExpressedAsAttribute)
            {
                XmlElement.RemoveAttribute(child.XmlElement.Name);
            }
            else
            {
                var previousSibling = child.XmlElement.PreviousSibling;

                XmlElement.RemoveChild(child.XmlElement);

                if (XmlDocument.PreserveWhitespace)
                {
                    //  If we are trying to preserve formatting of the file, then also remove any whitespace
                    //  that came before the node we removed.
                    if (previousSibling != null && previousSibling.NodeType == XmlNodeType.Whitespace)
                    {
                        XmlElement.RemoveChild(previousSibling);
                    }

                    //  If we removed the last non-whitespace child node, set IsEmpty to true so that we get:
                    //      <ItemName />
                    //  instead of:
                    //      <ItemName>
                    //      </ItemName>
                    if (XmlElement.HasChildNodes)
                    {
                        if (XmlElement.ChildNodes.Cast<XmlNode>().All(c => c.NodeType == XmlNodeType.Whitespace))
                        {
                            XmlElement.IsEmpty = true;
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Sets the first child in this container
        /// </summary>
        private void AddInitialChild(ProjectElement child)
        {
            ErrorUtilities.VerifyThrow(FirstChild == null && LastChild == null, "Expecting no children");

            VerifyForInsertBeforeAfterFirst(child, null);

            child.VerifyThrowInvalidOperationAcceptableLocation(this, null, null);

            child.Parent = this;

            FirstChild = child;
            LastChild = child;

            child.PreviousSibling = null;
            child.NextSibling = null;

            AddToXml(child);

            _count++;
            MarkDirty("Add child element named '{0}'", child.ElementName);
        }

        /// <summary>
        /// Common verification for insertion of an element.
        /// Reference may be null.
        /// </summary>
        private void VerifyForInsertBeforeAfterFirst(ProjectElement child, ProjectElement reference)
        {
            ErrorUtilities.VerifyThrowInvalidOperation(this.Parent != null || this.ContainingProject == this, "OM_ParentNotParented");
            ErrorUtilities.VerifyThrowInvalidOperation(reference == null || reference.Parent == this, "OM_ReferenceDoesNotHaveThisParent");
            ErrorUtilities.VerifyThrowInvalidOperation(child.Parent == null, "OM_NodeAlreadyParented");
            ErrorUtilities.VerifyThrowInvalidOperation(child.ContainingProject == this.ContainingProject, "OM_MustBeSameProject");

            // In RemoveChild() we do not update the victim's NextSibling (or PreviousSibling) to null, to allow RemoveChild to be
            // called within an enumeration. So we can't expect these to be null if the child was previously removed. However, we
            // can expect that what they point to no longer point back to it. They've been reconnected.
            ErrorUtilities.VerifyThrow(child.NextSibling == null || child.NextSibling.PreviousSibling != this, "Invalid structure");
            ErrorUtilities.VerifyThrow(child.PreviousSibling == null || child.PreviousSibling.NextSibling != this, "Invalid structure");
            VerifyThrowInvalidOperationNotSelfAncestor(child);
        }

        /// <summary>
        /// Verifies that the provided element isn't this element or a parent of it.
        /// If it is, throws InvalidOperationException.
        /// </summary>
        private void VerifyThrowInvalidOperationNotSelfAncestor(ProjectElement element)
        {
            ProjectElement ancestor = this;

            while (ancestor != null)
            {
                ErrorUtilities.VerifyThrowInvalidOperation(ancestor != element, "OM_SelfAncestor");
                ancestor = ancestor.Parent;
            }
        }

        /// <summary>
        /// Recurses into the provided container (such as a choose) and finds all child elements, even if nested.
        /// Result does NOT include the element passed in.
        /// The caller could filter these.
        /// </summary>
        private IEnumerable<ProjectElement> GetChildrenRecursively()
        {
            ProjectElement child = FirstChild;

            while (child != null)
            {
                yield return child;

                ProjectElementContainer container = child as ProjectElementContainer;

                if (container != null)
                {
                    foreach (ProjectElement grandchild in container.AllChildren)
                    {
                        yield return grandchild;
                    }
                }

                child = child.NextSibling;
            }
        }

        /// <summary>
        /// Enumerable over a series of sibling ProjectElement objects
        /// </summary>
        private struct ProjectElementSiblingEnumerable : IEnumerable<ProjectElement>
        {
            /// <summary>
            /// The enumerator
            /// </summary>
            private ProjectElementSiblingEnumerator _enumerator;

            /// <summary>
            /// Constructor
            /// </summary>
            internal ProjectElementSiblingEnumerable(ProjectElement initial)
                : this(initial, true)
            {
            }

            /// <summary>
            /// Constructor allowing reverse enumeration
            /// </summary>
            internal ProjectElementSiblingEnumerable(ProjectElement initial, bool forwards)
            {
                _enumerator = new ProjectElementSiblingEnumerator(initial, forwards);
            }

            /// <summary>
            /// Get enumerator
            /// </summary>
            public IEnumerator<ProjectElement> GetEnumerator()
            {
                return _enumerator;
            }

            /// <summary>
            /// Get non generic enumerator
            /// </summary>
            System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
            {
                return _enumerator;
            }

            /// <summary>
            /// Enumerator over a series of sibling ProjectElement objects
            /// </summary>
            private struct ProjectElementSiblingEnumerator : IEnumerator<ProjectElement>
            {
                /// <summary>
                /// First element
                /// </summary>
                private ProjectElement _initial;

                /// <summary>
                /// Current element
                /// </summary>
                private ProjectElement _current;

                /// <summary>
                /// Whether enumeration should go forwards or backwards.
                /// If backwards, the "initial" will be the first returned, then each previous
                /// node in turn.
                /// </summary>
                private bool _forwards;

                /// <summary>
                /// Constructor taking the first element
                /// </summary>
                internal ProjectElementSiblingEnumerator(ProjectElement initial, bool forwards)
                {
                    _initial = initial;
                    _current = null;
                    _forwards = forwards;
                }

                /// <summary>
                /// Current element
                /// Returns null if MoveNext() hasn't been called
                /// </summary>
                public ProjectElement Current
                {
                    get { return _current; }
                }

                /// <summary>
                /// Current element.
                /// Throws if MoveNext() hasn't been called
                /// </summary>
                object System.Collections.IEnumerator.Current
                {
                    get
                    {
                        if (_current != null)
                        {
                            return _current;
                        }

                        throw new InvalidOperationException();
                    }
                }

                /// <summary>
                /// Dispose. Do nothing.
                /// </summary>
                public void Dispose()
                {
                }

                /// <summary>
                /// Moves to the next item if any, otherwise returns false
                /// </summary>
                public bool MoveNext()
                {
                    ProjectElement next;

                    if (_current == null)
                    {
                        next = _initial;
                    }
                    else
                    {
                        next = _forwards ? _current.NextSibling : _current.PreviousSibling;
                    }

                    if (next != null)
                    {
                        _current = next;
                        return true;
                    }

                    return false;
                }

                /// <summary>
                /// Return to start
                /// </summary>
                public void Reset()
                {
                    _current = null;
                }
            }
        }
    }
}
