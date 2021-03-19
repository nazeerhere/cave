import React, { Component, useEffect, useState } from "react";
import DeleteModal from "./DeleteModal"
import EditModal from "./EditModal"
import Likes from "../Likes"

export default function Comment() {
    const [comments, setComment] = useState([])
    const [id, setId] = useState(0)
    const [comLikes, setLikes] = useState(0)
    
    const showEdit = (event) => {
        let modal = document.getElementById("EditModal")
        modal.style.opacity = 1;
        modal.style.zIndex = 2;
        setId(event.target.value)

    }

    const showDelete = (event) => {
        let modal = document.getElementById("DeleteModal")
        modal.style.opacity = 1;
        modal.style.zIndex = 2;
        setId(event.target.value)

    }

    let name14
    useEffect(() => {
        async function name12() {
            let name13 =  await fetch("https://uncool-backend.herokuapp.com/comments")
            name14 = await name13.json()
            setComment(name14)
        }
        name12()
    }, [comments]);

    return(
        <div className="comment" >

        {
            comments.length > 0
            &&
            comments.map(comment => {
                // setLikes(comment.likes)
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
                                <Likes comLikes={comment.likes} setLikes={setLikes} />
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
        </div>
    )
}