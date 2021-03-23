import Comment from "./Comment"
import InputBar from "./InputBar"
import Filter from "./Filter"
import React, { useState, useEffect} from "react"

export default function CommentsBar() {
    const [comments, setComment] = useState([])
    const [filteredText, setFilter] = useState("")

    useEffect(() => {
        async function name12() {
            let name13 =  await fetch("https://uncool-backend.herokuapp.com/comments")
            let name14 = await name13.json()
            setComment(name14)
        }
        name12()
    }, [comments])

    useEffect(() => {
        setFilter(comments)
    }, [])
    return(
        <div className="CommentsBar" >
            <p id="comText">
                Comments
            </p>
            <Filter comments={comments} 
                    setComment={setComment} 
                    filteredText={filteredText} 
                    setFilter={setFilter}
             />
            <Comment filteredText={filteredText} 
            />
            <InputBar/>
        </div>
    )
}