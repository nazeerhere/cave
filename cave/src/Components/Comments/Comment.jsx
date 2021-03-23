import React, { Component, useEffect, useState } from "react";
import DeleteModal from "./DeleteModal"
import EditModal from "./EditModal"
import Likes from "../Likes"

export default function Comment({ filteredText }) {
    const [id, setId] = useState(0)
    
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

    return(
        <div className="comment" >

        {
            filteredText.length > 0
            &&
            filteredText.map(comment => {
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
                                <Likes fakeNumber={comment.likes}/>
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