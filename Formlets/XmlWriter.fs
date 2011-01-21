﻿namespace Formlets

type xml_item = 
    | Text of string 
    | Tag of string * (string*string) list * xml_item list // tagname, attributes, children    

type 'a XmlWriter = xml_item list * 'a

/// Applicative functor that manipulates HTML as XML
module XmlWriter =
    let inline puree v : 'a XmlWriter = [],v
    //let ap (x: xml_item list,f) (y,a) = x @ y, f a
    let ap (f: ('a -> 'b) XmlWriter) (x: 'a XmlWriter) : 'b XmlWriter =
        let ff = fst f
        let sf = snd f
        let fx = fst x
        let sx = snd x
        ff @ fx, sf sx
    let inline (<*>) f x = ap f x
    let inline lift f x = puree f <*> x
    let inline lift2 f x y = puree f <*> x <*> y
    let inline plug (k: xml_item list -> xml_item list) (v: 'a XmlWriter): 'a XmlWriter = 
        k (fst v), snd v
    let inline xml (e: xml_item list) : unit XmlWriter = 
        plug (fun _ -> e) (puree ())
    let inline text s = xml [Text s]
    let inline tag name attributes (v: 'a XmlWriter) : 'a XmlWriter = 
        plug (fun x -> [Tag (name, attributes, x)]) v
    let emptyElems = ["area";"base";"basefont";"br";"col";"frame";"hr";"img";"input";"isindex";"link";"meta";"param"]

    open System.Xml.Linq
    let xelem (e: XNode) : unit XmlWriter =
        let rec xelem' (e: XNode) =
            let attr (x: XAttribute) = x.Name.LocalName, x.Value
            let elem (e: XElement) =
                let a = e.Attributes() |> Seq.map attr |> Seq.toList
                let name = e.Name.LocalName
                let children = e.Nodes() |> Seq.collect xelem' |> Seq.toList
                Tag(name,a,children)
            match e with
            | :? XComment -> []
            | :? XText as t -> [Text t.Value]
            | :? XElement as e -> [elem e]
            | :? XDocument as d -> [elem d.Document.Root]
            | _ -> failwithf "Unknown element %A" e
        xml (xelem' e)

    let render xml =
        let (!!) x = XName.op_Implicit x
        let xattr (name, value: string) = XAttribute(!!name, value)
        let xelem name (attributes: XObject list) (children: XNode list) = 
            let isEmpty = List.exists ((=) name) emptyElems
            let children = 
                match children,isEmpty with
                | [],false -> [(XText "") :> XObject]
                | _ -> List.map (fun x -> upcast x) children
            XElement(!!name, attributes @ children)
        let rec renderForest x =
            let render' =
                function
                | Text t -> XText t :> XNode
                | Tag (name, attr, children) -> 
                    let attr = List.map (fun a -> xattr a :> XObject) attr
                    let children = renderForest children
                    upcast (xelem name attr children)
            List.map render' x
        let nodes = renderForest xml
        let root = 
            match nodes with
            | [x] -> x
            | [] -> null
            | x::xs -> upcast (xelem "div" [] (x::xs))
        XDocument root
