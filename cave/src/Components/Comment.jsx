import React, { useEffect, useState } from "react";
import Likes from "./Likes"

export default function Comment() {
    const [comments, setComment] = useState([])

    const commentArr = []
    const dumb = ["List of task to complete for the project", "fix the input bars", "fix how to game renders to the page", "fix the login", "fix the comment url in the backend"]

    useEffect(() => {
        async function name12() {
            let name13 =  await fetch("http://localhost:7000/comments")
            let name14 = await name13.json()
            console.log(name14)
            setComment(name14)
        }
        name12()
    }, []);
    console.log(comments)

    return(
        <div className="comment" >

        {
            comments.length > 0
            &&
            comments.map(comment => {
                console.log(comment)
                console.log(comment.comment)
                return(
                    <div className="daBorder" >
                        {comment.user} -
                        <br/>
                        <br/>
                        {comment.comment}
                        {/* <span>
                            <Likes  props={comment.likes} />
                        </span> */}
                    </div>
                )
            })
        }

        </div>
    )
}