import React, { useEffect, useState } from "react";
import Likes from "./Likes"
import axios from "axios"

export default function Comment() {
    const [comments, setComment] = useState([])
    const [newComment, setNew] = useState({
        Usser: "Ye",
        comment: "",
        likes: 0
    })

    const handleSubmit = () => {
        axios.put(
            "https://uncool-backend.herokuapp.com/comments",
            newComment
        )
        console.log(newComment)
        setNew({
            user: "",
            comment: "",
            likes: 0
        })
        console.log("COMMENT SENT")
    }

    const handleComment = (event) => {
        const userInput = event.target.value
        setNew(prevState => {
            return {
                ...prevState,
                comment: userInput
            }
        })
    }

    const dumb = ["List of task to complete for the project", "fix the input bars", "fix how to game renders to the page", "fix the login", "fix the comment url in the backend"]

    useEffect(() => {
        async function name12() {
            let name13 =  await fetch("https://uncool-backend.herokuapp.com/comments")
            let name14 = await name13.json()
            console.log(name14)
            setComment(name14)
        }
        name12()
    }, []);
    console.log(newComment)

    return(
        <div className="comment" >

        {
            comments.length > 0
            &&
            comments.map(comment => {
                console.log(comment)
                console.log(comment.comment)
                return(
                    <div >
                    <div className="daBorder" >
                        {comment.user} -
                        <br/>
                        <br/>
                        {comment.comment}
                        {/* <span>
                            <Likes  props={comment.likes} />
                        </span> */}
                    </div>
                    <span id="btnHold" >

                        <button className="crud"  
                                // onClick={}
                        >edit
                        </button>

                        <button className="crud"  
                                // onClick={}
                        >delete
                        </button>

                    </span>
                    </div>
                )
            })
        }

        <div className="leaveComment" >
            <input type="text"
                    id="typing" 
                    placeholder="Leave a comment.."
                    onChange={handleComment}
            />

           <button id="submit" 
                    onClick={handleSubmit}
           >Submit</button>
        </div>
        </div>
    )
}