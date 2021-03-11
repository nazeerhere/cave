import React, { Component, useEffect, useState } from "react";
import DeleteModal from "./DeleteModal"
import EditModal from "./EditModal"
import Likes from "../Likes"
import axios from "axios"

export default function Comment() {

    const initialState = {
        id: 0,
        user: 3,
        comment: "",
        likes: 0
    }

    const [comments, setComment] = useState([])
    const [newComment, setNew] = useState(initialState)
    const [charactersUsed, setChars] = useState(0)
    const [id, setId] = useState(0)

    const handleSubmit = (event) => {
        event.preventDefault();
        console.log(newComment)

        if (charactersUsed <= 300) {
            axios.post(
                "https://uncool-backend.herokuapp.com/comments/",
                newComment
            )
            setNew(initialState)
            console.log("COMMENT SENT")
            document.getElementById("typing").value = ""
            
        } else {
            console.log("over the character limit")
        }
        setChars(0)        
    }
    
    const handleComment = (event) => {
        const userInput = event.target.value
        console.log(newComment)
        setNew(prevState => {
            return {
                ...prevState,
                comment: userInput
            }
        })
        let newNum = userInput.length
        setChars(newNum)
    }

    const showEdit = (event) => {
        // let modal = document.getElementsByClassName("comment")
        let modal = document.getElementById("EditModal")
        modal.style.opacity = 1;
        setId(event.target.value)

    }

    const showDelete = (event) => {
        // let modal = document.getElementsByClassName("comment")
        let modal = document.getElementById("DeleteModal")
        modal.style.opacity = 1;
        setId(event.target.value)

    }

    let name14
    useEffect(() => {
        async function name12() {
            let name13 =  await fetch("https://uncool-backend.herokuapp.com/comments")
            name14 = await name13.json()
            // console.log(name14)
            setComment(name14)
            console.log(name14)
        }
        name12()
    }, []);
    // console.log(newComment)

    return(
        <div className="comment" >

        {
            comments.length > 0
            &&
            comments.map(comment => {
                console.log(comment)
                // console.log(comment.comment)
                return(
                    <div >
                    <div className="daBorder" >
                        {comment.user} -
                        <br/>
                        <br/>
                        <span className="comText" >
                            {comment.comment}
                        </span>
                        <span>
                            <Likes liken={comment.likes} />
                        </span>
                    </div>
                    <span id="btnHold" >

                        <button className="crud"  
                                onClick={showEdit}
                                value={comment.id}
                        >edit
                        </button>

                        <button className="crud"  
                                onClick={showDelete}
                                value={comment.id}
                        >delete
                        </button>

                    </span>
                    </div>
                )
            })
        }

        <EditModal id={id}  style="color: white; background-color: white;"  />
        <DeleteModal id={id} />
        <div className="leaveComment" >
        <p id="charLimit">
        character limit: 
        {charactersUsed} 
         /300
        </p>
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